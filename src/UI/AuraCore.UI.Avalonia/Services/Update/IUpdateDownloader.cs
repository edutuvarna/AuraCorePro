namespace AuraCore.UI.Avalonia.Services.Update;

public sealed record AvailableUpdate(string Version, string DownloadUrl, string Sha256, bool IsMandatory);

public interface IUpdateDownloader
{
    /// <summary>Streams to %TEMP%, verifies SHA256, returns absolute file path.</summary>
    Task<string> DownloadAsync(AvailableUpdate update, IProgress<double> progress, CancellationToken ct);

    /// <summary>Starts the installer and exits the current process.</summary>
    void InstallAndExit(string installerPath);
}
