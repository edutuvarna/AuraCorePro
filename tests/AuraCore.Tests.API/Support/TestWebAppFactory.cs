#if PHASE_6_12_CAPTCHA_READY
using AuraCore.API.Application.Services.Security;
#endif
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

            // Phase 6.12 — captcha substitution wired in Task 2 once
            // ICaptchaVerifier is created. Until then, this is dead.
            #if PHASE_6_12_CAPTCHA_READY
            var captchaDesc = services.SingleOrDefault(d => d.ServiceType == typeof(ICaptchaVerifier));
            if (captchaDesc is not null) services.Remove(captchaDesc);
            services.AddSingleton<ICaptchaVerifier, AlwaysAllowCaptchaVerifier>();
            #endif
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
