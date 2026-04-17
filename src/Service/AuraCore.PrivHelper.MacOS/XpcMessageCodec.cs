using AuraCore.PrivHelper.MacOS.Interop;
using System.Runtime.InteropServices;

namespace AuraCore.PrivHelper.MacOS;

// ---------------------------------------------------------------------------
// XPC wire-protocol records
// ---------------------------------------------------------------------------

/// <summary>Decoded inbound XPC request from the client app.</summary>
internal sealed record XpcRequest(
    string ActionId,
    string[] Arguments,
    long TimeoutSeconds);

/// <summary>Outbound XPC reply from the daemon to the client app.</summary>
internal sealed record XpcReply(
    long ExitCode,
    string Stdout,
    string Stderr,
    string AuthState);

// ---------------------------------------------------------------------------
// XpcMessageCodec
// ---------------------------------------------------------------------------

/// <summary>
/// Encodes/decodes XPC dictionaries to/from <see cref="XpcRequest"/> and
/// <see cref="XpcReply"/> records.
/// <para>
/// <b>Strict key allowlist (spec §3.2):</b> inbound dictionaries must contain
/// exactly the three keys <c>action_id</c>, <c>args</c>, <c>timeout_seconds</c>.
/// Any extra or missing keys cause <see cref="DecodeXpcDictionary"/> to return
/// <c>null</c> (malformed — caller should reply with exit=-101).
/// </para>
/// <para>
/// <b>Fake mode</b> (tests on Windows dev host): <see cref="EncodeToFake"/> /
/// <see cref="DecodeFromFake"/> marshal through a
/// <see cref="Dictionary{TKey,TValue}"/> instead of real xpc_dictionary pointers,
/// exercising all schema-validation logic without libxpc.
/// </para>
/// </summary>
internal static class XpcMessageCodec
{
    // -----------------------------------------------------------------------
    // Allowed key sets (strict allowlist per spec §3.2)
    // -----------------------------------------------------------------------

    private static readonly HashSet<string> AllowedInboundKeys =
        new(StringComparer.Ordinal) { "action_id", "args", "timeout_seconds" };

    // -----------------------------------------------------------------------
    // Timeout clamping
    // -----------------------------------------------------------------------

    private const long MinTimeoutSeconds = 1L;
    private const long MaxTimeoutSeconds = HelperRuntimeOptions.MaxAllowedTimeoutSeconds;

    // -----------------------------------------------------------------------
    // Real xpc_dictionary decode (macOS production path)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Decodes an incoming <c>xpc_dictionary</c> pointer into an
    /// <see cref="XpcRequest"/>. Returns <c>null</c> on any schema violation.
    /// </summary>
    internal static XpcRequest? DecodeXpcDictionary(IntPtr xdict)
    {
        if (xdict == IntPtr.Zero) return null;

        try
        {
            // action_id
            var actionIdPtr = LibXpc.xpc_dictionary_get_string(xdict, "action_id");
            if (actionIdPtr == IntPtr.Zero) return null;
            var actionId = Marshal.PtrToStringUTF8(actionIdPtr);
            if (string.IsNullOrWhiteSpace(actionId)) return null;

            // args (xpc_array of strings)
            var argsPtr = LibXpc.xpc_dictionary_get_array(xdict, "args");
            string[] args;
            if (argsPtr == IntPtr.Zero)
            {
                args = Array.Empty<string>();
            }
            else
            {
                var count = (int)LibXpc.xpc_array_get_count(argsPtr);
                args = new string[count];
                for (int i = 0; i < count; i++)
                {
                    var elemPtr = LibXpc.xpc_array_get_string(argsPtr, (nuint)i);
                    if (elemPtr == IntPtr.Zero) return null;    // non-string element
                    var elem = Marshal.PtrToStringUTF8(elemPtr);
                    if (elem is null) return null;
                    args[i] = elem;
                }
            }

            // timeout_seconds
            var timeout = LibXpc.xpc_dictionary_get_int64(xdict, "timeout_seconds");
            timeout = ClampTimeout(timeout);

            return new XpcRequest(actionId, args, timeout);
        }
        catch
        {
            // Any interop error → treat as malformed
            return null;
        }
    }

    // -----------------------------------------------------------------------
    // Real xpc_dictionary encode (macOS production path)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Encodes an <see cref="XpcReply"/> into a new <c>xpc_dictionary</c> pointer.
    /// Caller takes ownership and is responsible for XPC object lifecycle.
    /// </summary>
    internal static IntPtr EncodeXpcDictionary(XpcReply reply)
    {
        var xdict = LibXpc.xpc_dictionary_create(IntPtr.Zero, IntPtr.Zero, 0);
        if (xdict == IntPtr.Zero) return IntPtr.Zero;

        LibXpc.xpc_dictionary_set_int64(xdict, "exit_code", reply.ExitCode);
        LibXpc.xpc_dictionary_set_string(xdict, "stdout", reply.Stdout ?? string.Empty);
        LibXpc.xpc_dictionary_set_string(xdict, "stderr", reply.Stderr ?? string.Empty);
        LibXpc.xpc_dictionary_set_string(xdict, "auth_state", reply.AuthState ?? "rejected");

        return xdict;
    }

    // -----------------------------------------------------------------------
    // Fake (in-memory) mode — test harness on Windows dev host
    // -----------------------------------------------------------------------

    /// <summary>
    /// Encodes an <see cref="XpcRequest"/> into an in-memory dictionary for
    /// testing. Mirrors the exact key names and value types of the wire format.
    /// </summary>
    internal static Dictionary<string, object> EncodeToFake(XpcRequest request)
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["action_id"] = request.ActionId,
            ["args"] = request.Arguments.Cast<object>().ToArray(),
            ["timeout_seconds"] = request.TimeoutSeconds,
        };
    }

    /// <summary>
    /// Decodes an in-memory fake dictionary (produced by test code) into an
    /// <see cref="XpcRequest"/>. Applies the same strict-key-allowlist and
    /// validation rules as the real <see cref="DecodeXpcDictionary"/>.
    /// Returns <c>null</c> on any schema violation.
    /// </summary>
    internal static XpcRequest? DecodeFromFake(Dictionary<string, object> dict)
    {
        if (dict is null) return null;

        // Strict key allowlist: reject extra keys
        foreach (var key in dict.Keys)
        {
            if (!AllowedInboundKeys.Contains(key))
                return null;
        }

        // action_id — required, must be non-empty string
        if (!dict.TryGetValue("action_id", out var actionIdObj)) return null;
        if (actionIdObj is not string actionId || string.IsNullOrWhiteSpace(actionId)) return null;

        // args — required (may be empty array), all elements must be strings
        if (!dict.TryGetValue("args", out var argsObj)) return null;
        string[] args;
        switch (argsObj)
        {
            case string[] strArr:
                args = strArr;
                break;
            case object[] objArr:
                // Validate each element is a string
                var validated = new string[objArr.Length];
                for (int i = 0; i < objArr.Length; i++)
                {
                    if (objArr[i] is not string s) return null;
                    validated[i] = s;
                }
                args = validated;
                break;
            default:
                return null;
        }

        // timeout_seconds — required, must be numeric (long or int)
        if (!dict.TryGetValue("timeout_seconds", out var timeoutObj)) return null;
        long timeout;
        switch (timeoutObj)
        {
            case long l: timeout = l; break;
            case int i: timeout = i; break;
            default: return null;
        }
        timeout = ClampTimeout(timeout);

        return new XpcRequest(actionId, args, timeout);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static long ClampTimeout(long raw) =>
        Math.Clamp(raw, MinTimeoutSeconds, MaxTimeoutSeconds);
}
