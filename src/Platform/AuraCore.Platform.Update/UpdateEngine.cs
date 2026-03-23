using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application.Interfaces.Platform;
namespace AuraCore.Platform.Update;

public sealed class UpdateEngine : IUpdateEngine
{
    public Task<UpdateManifest?> CheckAsync(CancellationToken ct = default) => Task.FromResult<UpdateManifest?>(null);
    public Task DownloadAsync(UpdateManifest manifest, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default) => Task.CompletedTask;
    public Task<UpdateResult> InstallAsync(CancellationToken ct = default) => Task.FromResult(new UpdateResult(true, null));
    public Task RollbackAsync(CancellationToken ct = default) => Task.CompletedTask;
}

public static class UpdateRegistration
{
    public static IServiceCollection AddUpdateEngine(this IServiceCollection services) => services.AddSingleton<IUpdateEngine, UpdateEngine>();
}
