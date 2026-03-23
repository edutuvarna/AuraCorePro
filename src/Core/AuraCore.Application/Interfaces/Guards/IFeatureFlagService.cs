namespace AuraCore.Application.Interfaces.Guards;

public interface IFeatureFlagService
{
    bool IsEnabled(string flagName);
    T GetValue<T>(string flagName, T defaultValue);
    Task RefreshAsync(CancellationToken ct = default);
}
