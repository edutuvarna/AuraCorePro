using System.Runtime.CompilerServices;

// Grant the platform test project access to internal types (Interop.LibXpc,
// Interop.SecCode, PeerVerifier, XpcMessageCodec, etc.) for reflection-based
// API surface verification and unit testing.
// Production code never references these; they stay internal at runtime.
[assembly: InternalsVisibleTo("AuraCore.Tests.Platform")]

// Required by NSubstitute (Castle DynamicProxy) to generate mocks for
// internal interfaces (IPeerVerifier, IActionHandler, IAuditLogger).
// The assembly is not strong-named so this unkeyed form is sufficient.
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
