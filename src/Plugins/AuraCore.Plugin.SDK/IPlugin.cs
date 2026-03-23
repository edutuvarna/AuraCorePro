namespace AuraCore.Plugin.SDK;
public interface IPlugin
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    Task InitializeAsync(PluginContext context, CancellationToken ct = default);
    Task ActivateAsync(CancellationToken ct = default);
    Task DeactivateAsync(CancellationToken ct = default);
}
public sealed class PluginContext { }
