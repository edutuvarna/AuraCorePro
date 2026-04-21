using System.Text;
using AuraCore.API.Infrastructure;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=auracoredb;Username=postgres;Password=CHANGE_ME_IN_ENV";

// Production uses Supabase (set via appsettings.Production.json)
// Local dev uses localhost PostgreSQL (appsettings.json)

builder.Services.AddApiInfrastructure(connectionString);

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
            policy.WithOrigins(
                    "https://auracorepro.com",
                    "https://www.auracorepro.com",
                    "https://admin.auracorepro.com")
                .AllowAnyMethod()
                .AllowAnyHeader();
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

// GitHub release mirror (stub until 6.6.D wires OctokitReleaseMirror)
builder.Services.AddScoped<AuraCore.API.Application.Services.Releases.IGitHubReleaseMirror>(sp =>
    new AuraCore.API.Infrastructure.Services.Releases.NullGitHubReleaseMirror());

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 5_000_000; // 5 MB max
});

var app = builder.Build();

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

// Maintenance mode check
app.Use(async (context, next) =>
{
    // Skip maintenance check for admin endpoints, health, and auth
    var path = context.Request.Path.Value?.ToLower() ?? "";
    if (path.StartsWith("/api/admin/") || path.StartsWith("/health") || path.StartsWith("/api/auth/"))
    {
        await next();
        return;
    }

    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        var config = await db.AppConfigs.FirstOrDefaultAsync(c => c.Id == 1);
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
    }
    catch { /* If config check fails, don't block requests */ }

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

app.MapControllers();
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
