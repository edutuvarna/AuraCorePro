using System.Runtime.CompilerServices;

// Grant the platform test project access to internal types (Interop.LibXpc,
// Interop.SecCode) for reflection-based API surface verification.
// Production code never references these; they stay internal at runtime.
[assembly: InternalsVisibleTo("AuraCore.Tests.Platform")]
