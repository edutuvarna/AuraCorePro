using System;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.UI.Avalonia.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AuraCore.Tests.Integration.UrlGateway;

[Trait("Category", "Windows")]
public class UrlGatewayTests
{
    private static string UniquePipeName() => $"AuraCorePro.UrlGateway.Test.{Guid.NewGuid():N}";

    [Fact]
    public async Task Client_can_send_URL_to_server_and_server_fires_event()
    {
        if (!OperatingSystem.IsWindows()) return;

        var pipe = UniquePipeName();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        using var server = new UrlGatewayServer(NullLogger<UrlGatewayServer>.Instance, pipe);
        string? received = null;
        var done = new TaskCompletionSource<bool>();
        server.InstanceIntentReceived += (_, e) =>
        {
            received = e.Url;
            done.TrySetResult(true);
        };

        server.Start();
        await Task.Delay(200, cts.Token);

        var client = new UrlGatewayClient(pipe);
        var result = await client.SendAsync("auracore://disk-health", cts.Token);

        Assert.Equal(GatewaySendResult.Sent, result);
        await done.Task.WaitAsync(TimeSpan.FromSeconds(3), cts.Token);
        Assert.Equal("auracore://disk-health", received);
    }

    [Fact]
    public async Task Client_with_no_server_returns_NoServer()
    {
        if (!OperatingSystem.IsWindows()) return;

        var pipe = UniquePipeName();
        var client = new UrlGatewayClient(pipe);
        var result = await client.SendAsync("auracore://disk-health");
        Assert.Equal(GatewaySendResult.NoServer, result);
    }

    [Fact]
    public async Task Client_payload_over_limit_returns_PayloadTooLarge()
    {
        var pipe = UniquePipeName();
        var client = new UrlGatewayClient(pipe);
        var bigUrl = "auracore://" + new string('a', 2000);
        var result = await client.SendAsync(bigUrl);
        Assert.Equal(GatewaySendResult.PayloadTooLarge, result);
    }

    [Fact]
    public async Task Client_empty_url_returns_Failed()
    {
        var client = new UrlGatewayClient(UniquePipeName());
        var result = await client.SendAsync("");
        Assert.Equal(GatewaySendResult.Failed, result);
    }
}
