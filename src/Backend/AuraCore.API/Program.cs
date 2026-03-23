using System.Text;
using AuraCore.API.Infrastructure;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=auracoredb;Username=postgres;Password=10062005";

// Production uses Supabase (set via appsettings.Production.json)
// Local dev uses localhost PostgreSQL (appsettings.json)

builder.Services.AddApiInfrastructure(connectionString);

// CORS — allow desktop app from any origin
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? "AuraCorePro-Default-Secret-Key-Change-In-Production-Min32Chars!";

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

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", async (AuraCoreDbContext db) =>
{
    string dbStatus;
    try { await db.Database.CanConnectAsync(); dbStatus = "connected"; }
    catch { dbStatus = "error"; }
    return Results.Ok(new { status = "healthy", database = dbStatus, timestamp = DateTimeOffset.UtcNow, version = "1.0.0" });
});

app.Run();
