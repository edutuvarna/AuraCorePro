using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using AuraCore.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

public class SuperadminBootstrapServiceTests
{
    private static AuraCoreDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"boot-{Guid.NewGuid()}")
            .Options;
        return new AuraCoreDbContext(options);
    }

    [Fact]
    public async Task RunAsync_is_noop_when_env_var_unset()
    {
        Environment.SetEnvironmentVariable("SUPERADMIN_EMAILS", null);
        var db = BuildDb();
        db.Users.Add(new User { Id = Guid.NewGuid(), Email = "a@b.com", PasswordHash = "x", Role = "user" });
        await db.SaveChangesAsync();

        var svc = new SuperadminBootstrapService(db, NullLogger<SuperadminBootstrapService>.Instance);
        await svc.RunAsync();

        Assert.Equal("user", (await db.Users.FirstAsync()).Role);
    }

    [Fact]
    public async Task RunAsync_promotes_existing_user_to_superadmin()
    {
        Environment.SetEnvironmentVariable("SUPERADMIN_EMAILS", "boss@auracore.pro");
        var db = BuildDb();
        db.Users.Add(new User { Id = Guid.NewGuid(), Email = "boss@auracore.pro", PasswordHash = "x", Role = "admin" });
        await db.SaveChangesAsync();

        var svc = new SuperadminBootstrapService(db, NullLogger<SuperadminBootstrapService>.Instance);
        await svc.RunAsync();

        Assert.Equal("superadmin", (await db.Users.FirstAsync()).Role);
    }

    [Fact]
    public async Task RunAsync_is_idempotent_on_already_promoted_user()
    {
        Environment.SetEnvironmentVariable("SUPERADMIN_EMAILS", "boss@auracore.pro");
        var db = BuildDb();
        db.Users.Add(new User { Id = Guid.NewGuid(), Email = "boss@auracore.pro", PasswordHash = "x", Role = "superadmin" });
        await db.SaveChangesAsync();

        var svc = new SuperadminBootstrapService(db, NullLogger<SuperadminBootstrapService>.Instance);
        await svc.RunAsync();
        await svc.RunAsync();

        Assert.Equal(1, await db.Users.CountAsync(u => u.Role == "superadmin"));
    }

    [Fact]
    public async Task RunAsync_logs_warning_when_email_not_registered()
    {
        Environment.SetEnvironmentVariable("SUPERADMIN_EMAILS", "ghost@nowhere.com");
        var db = BuildDb();
        var logger = new ListLogger<SuperadminBootstrapService>();

        var svc = new SuperadminBootstrapService(db, logger);
        await svc.RunAsync();

        Assert.Contains(logger.Messages, m => m.Contains("not registered"));
    }

    [Fact]
    public async Task RunAsync_handles_multiple_comma_separated_emails_case_insensitive()
    {
        Environment.SetEnvironmentVariable("SUPERADMIN_EMAILS", " Alice@x.COM , bob@x.com ");
        var db = BuildDb();
        db.Users.Add(new User { Id = Guid.NewGuid(), Email = "alice@x.com", PasswordHash = "x", Role = "admin" });
        db.Users.Add(new User { Id = Guid.NewGuid(), Email = "bob@x.com", PasswordHash = "x", Role = "user" });
        await db.SaveChangesAsync();

        var svc = new SuperadminBootstrapService(db, NullLogger<SuperadminBootstrapService>.Instance);
        await svc.RunAsync();

        Assert.Equal(2, await db.Users.CountAsync(u => u.Role == "superadmin"));
    }
}

// Minimal in-memory logger to assert log content without Microsoft.Extensions.Logging.Testing
internal class ListLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
{
    public List<string> Messages { get; } = new();
    IDisposable? Microsoft.Extensions.Logging.ILogger.BeginScope<TState>(TState state) => null;
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Messages.Add(formatter(state, exception));
}
