using System.Text;
using AuraCore.API.Infrastructure;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddSignalR();

// Phase 6.9 hotfix: trust nginx's X-Forwarded-For + X-Forwarded-Proto so
// HttpContext.Connection.RemoteIpAddress reflects the actual client IP
// (not 127.0.0.1 from the nginx loopback). Nginx on origin already sends
// both headers (auracore-api + auracore-admin sites). Without this middleware,
// the admin panel's 'Whitelist My IP' shows 127.0.0.1, rate-limit counts all
// requests as coming from localhost, and audit_log.IpAddress is useless.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Clear the default restrictive networks; trust the loopback proxy explicitly.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    options.KnownProxies.Add(System.Net.IPAddress.Parse("127.0.0.1"));
    options.KnownProxies.Add(System.Net.IPAddress.Parse("::1"));
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=auracoredb;Username=postgres;Password=CHANGE_ME_IN_ENV";

// Production uses Supabase (set via appsettings.Production.json)
// Local dev uses localhost PostgreSQL (appsettings.json)

builder.Services.AddApiInfrastructure(connectionString);

builder.Services.AddScoped<AuraCore.API.Application.Services.Audit.IAuditLogService,
                          AuraCore.API.Infrastructure.Services.Audit.AuditLogService>();
builder.Services.AddScoped<AuraCore.API.Application.Services.Security.IWhitelistService,
                          AuraCore.API.Infrastructure.Services.Security.WhitelistService>();

// Phase 6.11 startup services
builder.Services.AddScoped<AuraCore.API.Services.SuperadminBootstrapService>();
builder.Services.AddScoped<AuraCore.API.Services.GrandfatherMigrationService>();

// Phase 6.11 T37: runtime-editable rate-limit policies
builder.Services.AddScoped<AuraCore.API.Application.Services.RateLimiting.IRateLimitConfigService,
                          AuraCore.API.Infrastructure.Services.RateLimiting.RateLimitConfigService>();

// Phase 6.11: transactional email via Resend HTTPS API
builder.Services.AddHttpClient("resend", client =>
{
    client.BaseAddress = new Uri("https://api.resend.com");
    var apiKey = Environment.GetEnvironmentVariable("RESEND_API_KEY")
        ?? builder.Configuration["Resend:ApiKey"];
    if (!string.IsNullOrEmpty(apiKey))
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
});
builder.Services.AddScoped<AuraCore.API.Application.Services.Email.IEmailService,
                           AuraCore.API.Infrastructure.Services.Email.ResendEmailService>();

// DataProtection with persistent keyring. Keys directory must be app-user-owned, chmod 600.
// On prod (Linux) default to /var/www/auracore-api/.dataprotection-keys; allow override via env var.
// On local dev the path won't exist — fall back to a temp dir so the app still boots.
var dpKeysPath = Environment.GetEnvironmentVariable("DATAPROTECTION_KEYS_PATH");
if (string.IsNullOrEmpty(dpKeysPath))
{
    dpKeysPath = builder.Environment.IsProduction()
        ? "/var/www/auracore-api/.dataprotection-keys"
        : Path.Combine(Path.GetTempPath(), "auracore-dp-keys-dev");
}
try { Directory.CreateDirectory(dpKeysPath); } catch { /* keys will be ephemeral if path not writable */ }
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath))
    .SetApplicationName("AuraCorePro");

builder.Services.AddScoped<AuraCore.API.Application.Services.Security.ITotpEncryption,
                          AuraCore.API.Infrastructure.Services.Security.TotpEncryption>();

// CORS — restrict origins in production
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        }
        else
        {
            // T3.17: production domain is auracore.pro (not auracorepro.com)
            // Phase 6.9 hotfix: AllowCredentials needed for SignalR + any other
            // cross-origin call that uses `credentials: 'include'` (admin panel
            // at admin.auracore.pro → api.auracore.pro). Without this,
            // browsers reject preflight responses with empty
            // Access-Control-Allow-Credentials header.
            policy.WithOrigins(
                    "https://auracore.pro",
                    "https://www.auracore.pro",
                    "https://admin.auracore.pro",
                    "https://download.auracore.pro")
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        }
    });
});

// JWT Authentication
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("JWT_SECRET env var or Jwt:Secret config must be set");
if (jwtSecret == "LOADED_FROM_ENV" || jwtSecret.Length < 32)
    throw new InvalidOperationException("JWT secret must be a real key with at least 32 characters");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // Keep "sub" as "sub", don't remap to NameIdentifier
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "AuraCorePro",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "AuraCorePro",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "sub",
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };

        // Phase 6.10 W4: SignalR WebSocket transport cannot set the Authorization
        // header from the browser. The frontend SignalR client passes the JWT via
        // ?access_token= query string (HubConnectionBuilder.accessTokenFactory).
        // Read it here ONLY for /hubs/ paths so non-hub endpoints keep
        // header-only auth (defense-in-depth: query-string tokens leak in
        // server logs / Referer headers, so we narrowly scope the exception).
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// R2 (S3-compatible) client for release binary storage
var r2AccountId = Environment.GetEnvironmentVariable("R2_ACCOUNT_ID") ?? "";
var r2AccessKey = Environment.GetEnvironmentVariable("R2_ACCESS_KEY_ID") ?? "";
var r2Secret    = Environment.GetEnvironmentVariable("R2_SECRET_ACCESS_KEY") ?? "";
var r2Bucket    = Environment.GetEnvironmentVariable("R2_BUCKET") ?? "auracore-releases";

builder.Services.AddSingleton<Amazon.S3.IAmazonS3>(_ =>
{
    var cfg = new Amazon.S3.AmazonS3Config
    {
        ServiceURL = $"https://{r2AccountId}.r2.cloudflarestorage.com",
        ForcePathStyle = true,  // required for R2
    };
    return new Amazon.S3.AmazonS3Client(r2AccessKey, r2Secret, cfg);
});
builder.Services.AddSingleton<AuraCore.API.Application.Services.Releases.IR2Client>(sp =>
    new AuraCore.API.Infrastructure.Services.Releases.AwsR2Client(
        sp.GetRequiredService<Amazon.S3.IAmazonS3>(), r2Bucket));

// GitHub release mirror via Octokit.NET (6.6.D)
builder.Services.AddSingleton<AuraCore.API.Application.Services.Releases.IGitHubReleaseMirror>(sp =>
    new AuraCore.API.Infrastructure.Services.Releases.OctokitReleaseMirror(
        sp.GetRequiredService<AuraCore.API.Application.Services.Releases.IR2Client>()));

// T1.20: Telemetry rate limiter — 60 events/min per IP, ephemeral in-memory state
builder.Services.AddSingleton<AuraCore.API.Application.Services.Telemetry.ITelemetryRateLimiter,
                              AuraCore.API.Infrastructure.Services.Telemetry.TelemetryRateLimiter>();

// T2.24: login_attempts retention sweep — purges rows older than 90 days once per 24h
builder.Services.AddHostedService<AuraCore.API.Infrastructure.Services.Audit.AuditLogPurgeService>();
builder.Services.AddHostedService<AuraCore.API.HostedServices.RetentionJob>();

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 5_000_000; // 5 MB max
});

var app = builder.Build();

// Trust nginx X-Forwarded-* headers BEFORE any middleware that reads the IP
// (CORS, auth, maintenance check, rate limiter). Must be the first app.Use call.
app.UseForwardedHeaders();

// Auto-migrate on startup (applies pending migrations to DB)
// Wrapped in try-catch: first run has no migrations yet, that's OK
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
    var pending = await db.Database.GetPendingMigrationsAsync();
    if (pending.Any())
    {
        await db.Database.MigrateAsync();
    }
}
catch (Exception ex)
{
    // Log but don't crash — migrations might not exist yet on first setup
    app.Logger.LogWarning("Auto-migrate skipped: {Message}. Run 'dotnet ef database update' manually.", ex.Message);
}

// Phase 6.11: superadmin bootstrap + grandfather migration (idempotent on every startup).
// Must run AFTER EF MigrateAsync so permission_grants table exists.
// Call order: bootstrap FIRST so a superadmin may exist when grandfather attributes grants.
try
{
    using var sa = app.Services.CreateScope();
    var bootstrap = sa.ServiceProvider.GetRequiredService<AuraCore.API.Services.SuperadminBootstrapService>();
    var grandfather = sa.ServiceProvider.GetRequiredService<AuraCore.API.Services.GrandfatherMigrationService>();
    await bootstrap.RunAsync();
    await grandfather.RunAsync();
}
catch (Exception ex)
{
    app.Logger.LogWarning("Phase 6.11 startup services skipped: {Msg}", ex.Message);
}

// T1.26: warn if extra app_configs rows exist (DB-level constraint added in Wave 1).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
    try
    {
        var extra = await db.AppConfigs.Where(c => c.Id != 1).CountAsync();
        if (extra > 0)
        {
            app.Logger.LogWarning("T1.26: found {Extra} extra AppConfig rows; only Id=1 is authoritative. Consider: DELETE FROM app_configs WHERE \"Id\" != 1;", extra);
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning("T1.26 singleton check skipped: {Msg}", ex.Message);
    }
}

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    if (!context.Request.IsHttps || context.Request.Host.Host != "localhost")
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    await next();
});

// Maintenance mode check — cached 30s, fails FAST (503) on DB error so an
// accidentally-unreachable app_configs table doesn't silently disable the
// maintenance banner (configuration.md F-4).
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLower() ?? "";
    // Skip maintenance check for admin endpoints, health, and auth
    if (path.StartsWith("/api/admin/") || path.StartsWith("/health") || path.StartsWith("/api/auth/"))
    {
        await next();
        return;
    }

    var cache = context.RequestServices.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
    AuraCore.API.Domain.Entities.AppConfig? config;
    try
    {
        config = await cache.GetOrCreateAsync<AuraCore.API.Domain.Entities.AppConfig?>(
            "maintenance-config",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
                var db = context.RequestServices.GetRequiredService<AuraCoreDbContext>();
                return await db.AppConfigs.FirstOrDefaultAsync(c => c.Id == 1);
            });
    }
    catch
    {
        // Fail FAST: DB unreachable → 503 (not fail-open) so an actual maintenance
        // mode toggle can't be silently nullified by a DB hiccup.
        context.Response.StatusCode = 503;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(new { error = "Configuration unavailable" }));
        return;
    }

    if (config?.IsMaintenanceMode == true)
    {
        context.Response.StatusCode = 503;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                error = "Service is under maintenance",
                message = config.MaintenanceMessage ?? "AuraCore Pro is currently under maintenance. Please try again later."
            }));
        return;
    }

    await next();
});

// HTTPS redirect handled by Nginx reverse proxy in production
if (!app.Environment.IsDevelopment() && !app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Phase 6.11: reject requests whose JWT jti is blacklisted.
// MUST be after UseAuthentication so HttpContext.User has claims.
// Other Phase 6.11 middlewares (ScopeLimitedTokenMiddleware, ForcePasswordChangeMiddleware)
// are added in Wave 5 immediately after this line.
app.UseMiddleware<AuraCore.API.Middleware.TokenRevocationMiddleware>();
app.UseMiddleware<AuraCore.API.Middleware.ScopeLimitedTokenMiddleware>();
app.UseMiddleware<AuraCore.API.Middleware.ForcePasswordChangeMiddleware>();

app.MapControllers();
app.MapHub<AuraCore.API.Hubs.AdminHub>("/hubs/admin");
app.MapGet("/health", async (AuraCoreDbContext db) =>
{
    string dbStatus;
    try { await db.Database.CanConnectAsync(); dbStatus = "connected"; }
    catch { dbStatus = "error"; }
    if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production")
    {
        return Results.Ok(new { status = "healthy", database = dbStatus });
    }
    return Results.Ok(new { status = "healthy", database = dbStatus, timestamp = DateTimeOffset.UtcNow, version = "1.7.0" });
});

app.Run();

// Expose Program as a public partial type so WebApplicationFactory<Program>
// in integration tests can resolve it. Without this, top-level Program is
// internal-only and the test project would need InternalsVisibleTo gymnastics
// that don't actually work with generic type parameters.
public partial class Program { }
