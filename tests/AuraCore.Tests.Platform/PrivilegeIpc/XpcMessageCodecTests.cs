using AuraCore.PrivHelper.MacOS;
using FluentAssertions;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

/// <summary>
/// Tests for <see cref="XpcMessageCodec"/> using the in-memory fake mode
/// (marshals through <see cref="Dictionary{TKey,TValue}"/> instead of real
/// xpc_dictionary pointers). This lets schema-validation rules be exercised
/// on any dev host without libxpc.
/// </summary>
public class XpcMessageCodecTests
{
    [Fact]
    public void Encode_then_decode_roundtrips_xpc_request_faithfully()
    {
        var req     = new XpcRequest("dns-flush", new[] { "-flushcache" }, 30);
        var fake    = XpcMessageCodec.EncodeToFake(req);
        var decoded = XpcMessageCodec.DecodeFromFake(fake);

        decoded.Should().NotBeNull();
        decoded!.ActionId.Should().Be("dns-flush");
        decoded.Arguments.Should().BeEquivalentTo(new[] { "-flushcache" });
        decoded.TimeoutSeconds.Should().Be(30);
    }

    [Fact]
    public void Decode_rejects_message_with_extra_keys()
    {
        // Spec §3.2: strict key allowlist — extra keys = reject.
        var fake = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["action_id"]        = "dns-flush",
            ["args"]             = new[] { "-flushcache" },
            ["timeout_seconds"]  = 30L,
            ["evil_sidechannel"] = "rm -rf /",
        };
        XpcMessageCodec.DecodeFromFake(fake).Should().BeNull();
    }

    [Fact]
    public void Decode_rejects_message_with_missing_action_id()
    {
        var fake = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["args"]            = Array.Empty<string>(),
            ["timeout_seconds"] = 30L,
        };
        XpcMessageCodec.DecodeFromFake(fake).Should().BeNull();
    }

    [Fact]
    public void Decode_rejects_message_with_non_string_in_args_array()
    {
        var fake = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["action_id"]       = "dns-flush",
            ["args"]            = new object[] { "-flushcache", 999L },   // non-string element
            ["timeout_seconds"] = 30L,
        };
        XpcMessageCodec.DecodeFromFake(fake).Should().BeNull();
    }

    [Fact]
    public void Decode_clamps_wildly_large_timeout()
    {
        // Prevents malicious client from requesting a 99999999-second block.
        // Clamp ceiling = HelperRuntimeOptions.MaxAllowedTimeoutSeconds = 3600.
        var fake = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["action_id"]       = "dns-flush",
            ["args"]            = Array.Empty<string>(),
            ["timeout_seconds"] = 99_999_999L,
        };
        var decoded = XpcMessageCodec.DecodeFromFake(fake);
        decoded.Should().NotBeNull();
        decoded!.TimeoutSeconds.Should().BeLessOrEqualTo(3600);
    }

    [Fact]
    public void Decode_rejects_message_missing_timeout()
    {
        var fake = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["action_id"] = "dns-flush",
            ["args"]      = Array.Empty<string>(),
            // timeout_seconds intentionally omitted
        };
        XpcMessageCodec.DecodeFromFake(fake).Should().BeNull();
    }

    [Fact]
    public void Decode_accepts_empty_args_array()
    {
        var fake = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["action_id"]       = "dns-flush",
            ["args"]            = Array.Empty<string>(),
            ["timeout_seconds"] = 30L,
        };
        var decoded = XpcMessageCodec.DecodeFromFake(fake);
        decoded.Should().NotBeNull();
        decoded!.Arguments.Should().BeEmpty();
    }
}
