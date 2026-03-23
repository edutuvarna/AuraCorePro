using AuraCore.API.Application.Interfaces;
using AuraCore.API.Infrastructure.Data;
using AuraCore.API.Infrastructure.Repositories;
using AuraCore.API.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.API.Infrastructure;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddApiInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AuraCoreDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly("AuraCore.API.Infrastructure");
                npgsql.EnableRetryOnFailure(3);
            }));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ILicenseService, LicenseService>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<ITelemetryRepository, TelemetryRepository>();
        services.AddScoped<ICrashReportRepository, CrashReportRepository>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
