namespace AuraCore.Module.TimeMachineManager.Models;

public sealed record TimeMachineBackup(
    DateTime Date,
    string Path,
    long SizeBytes);
