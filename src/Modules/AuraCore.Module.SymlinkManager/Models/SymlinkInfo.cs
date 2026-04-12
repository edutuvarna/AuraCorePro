namespace AuraCore.Module.SymlinkManager.Models;

public enum SymlinkStatus { Valid, Broken, CircularRef }

public sealed record SymlinkInfo(
    string Path,
    string? Target,
    SymlinkStatus Status);
