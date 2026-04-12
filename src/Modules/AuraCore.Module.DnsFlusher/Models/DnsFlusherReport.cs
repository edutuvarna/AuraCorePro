namespace AuraCore.Module.DnsFlusher.Models;

public sealed record DnsFlusherReport(
    bool DscacheutilAvailable,
    DateTime? LastFlush);
