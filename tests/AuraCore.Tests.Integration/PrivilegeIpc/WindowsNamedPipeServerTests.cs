using System;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.IPC.Contracts;
using AuraCore.PrivilegedService.IPC;
using AuraCore.PrivilegedService.Ops;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AuraCore.Tests.Integration.PrivilegeIpc;

/// <summary>
/// Integration tests for <see cref="PipeServer"/>.
/// All tests are skipped on non-Windows platforms automatically.
/// </summary>
[Trait("Category", "Windows")]
public class WindowsNamedPipeServerTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static PipeServer BuildServer(string pipeName) =>
        new(pipeName,
            new DriverOperations(),
            new DefenderOperations(),
            new ServiceOperations(),
            NullLogger<PipeServer>.Instance);

    /// <summary>Connect a client, send a raw length-prefixed JSON payload, read back the response.</summary>
    private static async Task<JsonDocument> RoundTripAsync(
        string pipeName, byte[] rawJson, CancellationToken ct)
    {
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(3000, ct);

        await client.WriteAsync(BitConverter.GetBytes(rawJson.Length), ct);
        await client.WriteAsync(rawJson, ct);
        await client.FlushAsync(ct);

        var lenBuf = new byte[4];
        await client.ReadExactlyAsync(lenBuf, ct);
        int respLen = BitConverter.ToInt32(lenBuf);

        var respBuf = new byte[respLen];
        await client.ReadExactlyAsync(respBuf, ct);

        return JsonDocument.Parse(respBuf);
    }

    private static async Task<JsonDocument> RoundTripJsonAsync(
        string pipeName, object request, CancellationToken ct)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(request, request.GetType());
        return await RoundTripAsync(pipeName, payload, ct);
    }

    // ---------------------------------------------------------------------------
    // Test 1 — Ping
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Ping_request_receives_pong_response()
    {
        if (!OperatingSystem.IsWindows()) return;

        var pipeName = $"AuraCorePro-Test-{Guid.NewGuid():N}";
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var server     = BuildServer(pipeName);
        var serverTask = Task.Run(() => server.RunAsync(cts.Token), cts.Token);

        await Task.Delay(200, cts.Token); // let the server enter WaitForConnectionAsync

        var correlationId = Guid.NewGuid().ToString("N");
        var request       = new PingRequest(correlationId);

        using var doc = await RoundTripJsonAsync(pipeName, request, cts.Token);

        doc.RootElement.GetProperty("Success").GetBoolean()
            .Should().BeTrue(because: "ping must always succeed");
        doc.RootElement.GetProperty("CorrelationId").GetString()
            .Should().Be(correlationId, because: "CorrelationId must be echoed back exactly");

        cts.Cancel();
        try { await serverTask; } catch (OperationCanceledException) { }
    }

    // ---------------------------------------------------------------------------
    // Test 2 — Whitelist rejection
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Non_whitelisted_action_is_rejected()
    {
        if (!OperatingSystem.IsWindows()) return;

        var pipeName = $"AuraCorePro-Test-{Guid.NewGuid():N}";
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var server     = BuildServer(pipeName);
        var serverTask = Task.Run(() => server.RunAsync(cts.Token), cts.Token);

        await Task.Delay(200, cts.Token);

        // Craft a JSON body with a non-whitelisted ActionId
        var rawJson = Encoding.UTF8.GetBytes(
            "{\"CorrelationId\":\"test-corr-123\",\"ActionId\":\"system.format\"}");

        using var doc = await RoundTripAsync(pipeName, rawJson, cts.Token);

        doc.RootElement.GetProperty("Success").GetBoolean()
            .Should().BeFalse(because: "non-whitelisted actions must be rejected");
        doc.RootElement.GetProperty("Error").GetString()
            .Should().Contain("whitelist", because: "error message must indicate the whitelist rejection");
        doc.RootElement.GetProperty("CorrelationId").GetString()
            .Should().Be("test-corr-123", because: "CorrelationId must be echoed even for rejections");

        cts.Cancel();
        try { await serverTask; } catch (OperationCanceledException) { }
    }

    // ---------------------------------------------------------------------------
    // Test 3 — Oversized request is rejected
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Oversized_request_is_rejected()
    {
        if (!OperatingSystem.IsWindows()) return;

        var pipeName = $"AuraCorePro-Test-{Guid.NewGuid():N}";
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var server     = BuildServer(pipeName);
        var serverTask = Task.Run(() => server.RunAsync(cts.Token), cts.Token);

        await Task.Delay(200, cts.Token);

        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(3000, cts.Token);

        // Send a length field that exceeds the 64 KB cap without sending actual payload bytes.
        const int tooBig = 65 * 1024; // 65 KB
        await client.WriteAsync(BitConverter.GetBytes(tooBig), cts.Token);
        await client.FlushAsync(cts.Token);

        var lenBuf = new byte[4];
        await client.ReadExactlyAsync(lenBuf, cts.Token);
        int respLen = BitConverter.ToInt32(lenBuf);
        var respBuf = new byte[respLen];
        await client.ReadExactlyAsync(respBuf, cts.Token);

        using var doc = JsonDocument.Parse(respBuf);
        doc.RootElement.GetProperty("Success").GetBoolean()
            .Should().BeFalse(because: "oversized requests must be rejected before reading payload");

        cts.Cancel();
        try { await serverTask; } catch (OperationCanceledException) { }
    }

    // ---------------------------------------------------------------------------
    // Test 4 — Ping with no ActionId field (raw body without typed request)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Bare_CorrelationId_body_is_treated_as_ping()
    {
        if (!OperatingSystem.IsWindows()) return;

        var pipeName = $"AuraCorePro-Test-{Guid.NewGuid():N}";
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var server     = BuildServer(pipeName);
        var serverTask = Task.Run(() => server.RunAsync(cts.Token), cts.Token);

        await Task.Delay(200, cts.Token);

        // Only a CorrelationId — no ActionId
        var rawJson = Encoding.UTF8.GetBytes("{\"CorrelationId\":\"bare-ping\"}");

        using var doc = await RoundTripAsync(pipeName, rawJson, cts.Token);

        doc.RootElement.GetProperty("Success").GetBoolean()
            .Should().BeTrue(because: "bare CorrelationId body should be treated as ping");
        doc.RootElement.GetProperty("CorrelationId").GetString()
            .Should().Be("bare-ping");

        cts.Cancel();
        try { await serverTask; } catch (OperationCanceledException) { }
    }
}
