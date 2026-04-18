using System;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AuraCore.UI.Avalonia.Helpers;

public enum GatewaySendResult
{
    Sent,
    NoServer,
    PayloadTooLarge,
    Failed
}

/// <summary>
/// Sends a URL payload to the primary AuraCore instance over a
/// Named Pipe. Returns immediately with a status indicating whether
/// a server was reachable and acknowledged.
///
/// Caller contract: on Sent or Failed, exit the current process
/// (don't start another Avalonia window). On NoServer, proceed with
/// normal startup — caller is the primary instance.
/// </summary>
public sealed class UrlGatewayClient
{
    private readonly string _pipeName;
    private const int ConnectTimeoutMs = 500;
    private const int MaxPayloadBytes = 1024;

    public UrlGatewayClient(string pipeName = UrlGatewayServer.DefaultPipeName)
    {
        _pipeName = pipeName;
    }

    public async Task<GatewaySendResult> SendAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(url)) return GatewaySendResult.Failed;

        var payload = Encoding.UTF8.GetBytes(url);
        if (payload.Length == 0 || payload.Length > MaxPayloadBytes)
        {
            return GatewaySendResult.PayloadTooLarge;
        }

        try
        {
            using var client = new NamedPipeClientStream(
                ".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            try { await client.ConnectAsync(ConnectTimeoutMs, ct); }
            catch (TimeoutException) { return GatewaySendResult.NoServer; }
            catch (System.IO.FileNotFoundException) { return GatewaySendResult.NoServer; }
            catch (System.IO.IOException ex) when (ex.Message.IndexOf("pipe", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return GatewaySendResult.NoServer;
            }

            var length = (ushort)payload.Length;
            await client.WriteAsync(BitConverter.GetBytes(length), ct);
            await client.WriteAsync(payload, ct);
            await client.FlushAsync(ct);

            var ack = new byte[1];
            await client.ReadExactlyAsync(ack, ct);

            return ack[0] == 1 ? GatewaySendResult.Sent : GatewaySendResult.Failed;
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return GatewaySendResult.Failed;
        }
    }
}
