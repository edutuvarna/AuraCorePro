using Microsoft.Extensions.DependencyInjection;
namespace AuraCore.Infrastructure.Storage;
public sealed class LocalStorageService { }
public static class StorageRegistration
{
    public static IServiceCollection AddLocalStorage(this IServiceCollection services) => services.AddSingleton<LocalStorageService>();
}
