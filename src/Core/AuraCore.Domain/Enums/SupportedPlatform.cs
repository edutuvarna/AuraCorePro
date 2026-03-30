namespace AuraCore.Domain.Enums;

/// <summary>
/// Declares which operating system(s) a module supports.
/// Used by the DI container to register only relevant modules at startup.
/// </summary>
[Flags]
public enum SupportedPlatform
{
    Windows = 1,
    Linux   = 2,
    MacOS   = 4,
    All     = Windows | Linux | MacOS
}
