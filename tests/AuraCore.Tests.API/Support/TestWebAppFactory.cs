using AuraCore.API.Application.Services.Security;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.Tests.API.Support;

/// <summary>
/// Shared WebApplicationFactory configuration for all auth-touching tests.
/// Replaces the production DbContext with InMemory + a shared root, suppresses
/// the EF service-provider warning that fires when many fixtures coexist, and
/// substitutes ICaptchaVerifier with the always-allow stub.
/// </summary>
public sealed class TestWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName;
    private readonly InMemoryDatabaseRoot _dbRoot;

    public TestWebAppFactory()
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", "test-secret-at-least-32-characters-long!!");
        _dbName = $"test-{Guid.NewGuid()}";
        _dbRoot = new InMemoryDatabaseRoot();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var dbDesc = services.Single(d => d.ServiceType == typeof(DbContextOptions<AuraCoreDbContext>));
            services.Remove(dbDesc);
            services.AddDbContext<AuraCoreDbContext>(o => o
                .UseInMemoryDatabase(_dbName, _dbRoot)
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));

            // Phase 6.12 — substitute Turnstile verifier with always-allow stub.
            var captchaDesc = services.SingleOrDefault(d => d.ServiceType == typeof(ICaptchaVerifier));
            if (captchaDesc is not null) services.Remove(captchaDesc);
            services.AddSingleton<ICaptchaVerifier, AlwaysAllowCaptchaVerifier>();
        });
    }

    public async Task SeedAsync(Action<AuraCoreDbContext> act)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
        act(db);
        await db.SaveChangesAsync();
    }
}
