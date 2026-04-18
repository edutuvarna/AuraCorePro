using System;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AuraCore.UI.Avalonia.Helpers;

public sealed class InstanceIntentEventArgs : EventArgs
{
    public string Url { get; }
    public InstanceIntentEventArgs(string url) { Url = url; }
}

/// <summary>
/// Named Pipe server listening for URL payloads from secondary AuraCore
/// launch attempts. Each accepted connection fires InstanceIntentReceived
/// with the raw URL string. Consumers parse via UrlSchemeHandler.Parse.
///
/// Pipe ACL: current-user SID only (NOT Authenticated Users). A different
/// user session cannot inject URLs into this process's AuraCore.
///
/// Max payload: 1 KB. Larger requests rejected without processing.
/// </summary>
public sealed class UrlGatewayServer : IDisposable
{
    private readonly string _pipeName;
    private readonly ILogger<UrlGatewayServer> _logger;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private const int MaxPayloadBytes = 1024;

    public const string DefaultPipeName = "AuraCorePro.UrlGateway";

    public event EventHandler<InstanceIntentEventArgs>? InstanceIntentReceived;

    public UrlGatewayServer(ILogger<UrlGatewayServer> logger, string pipeName = DefaultPipeName)
    {
        _pipeName = pipeName;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Start()
    {
        if (_cts is not null) return; // already running
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try { _loopTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _cts?.Dispose();
        _cts = null;
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream stream;
            try { stream = CreatePipe(); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[url-gateway] failed to create pipe — retrying in 500ms");
                try { await Task.Delay(500, ct); } catch (OperationCanceledException) { break; }
                continue;
            }

            try
            {
                await stream.WaitForConnectionAsync(ct);
                _ = Task.Run(() => HandleConnectionAsync(stream, ct), CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                await stream.DisposeAsync();
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[url-gateway] accept failed");
                await stream.DisposeAsync();
            }
        }
    }

    private NamedPipeServerStream CreatePipe()
    {
        if (OperatingSystem.IsWindows())
        {
            var security = new PipeSecurity();
            var currentUser = WindowsIdentity.GetCurrent().User
                ?? throw new InvalidOperationException("current user SID unavailable");
            security.AddAccessRule(new PipeAccessRule(
                currentUser,
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));

            return NamedPipeServerStreamAcl.Create(
                _pipeName, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous,
                inBufferSize: MaxPayloadBytes, outBufferSize: 64, pipeSecurity: security);
        }

        return new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream stream, CancellationToken ct)
    {
        await using (stream)
        {
            try
            {
                // Read length-prefix (2 bytes, little-endian ushort).
                var lengthBuf = new byte[2];
                await stream.ReadExactlyAsync(lengthBuf, ct);
                int length = BitConverter.ToUInt16(lengthBuf);
                if (length <= 0 || length > MaxPayloadBytes)
                {
                    _logger.LogWarning("[url-gateway] rejected payload length {Length}", length);
                    return;
                }

                var payload = new byte[length];
                await stream.ReadExactlyAsync(payload, ct);

                var url = System.Text.Encoding.UTF8.GetString(payload);
                _logger.LogInformation("[url-gateway] received {Url}", url);

                // Ack
                var ack = new byte[] { 1 };
                await stream.WriteAsync(ack, ct);
                await stream.FlushAsync(ct);

                InstanceIntentReceived?.Invoke(this, new InstanceIntentEventArgs(url));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[url-gateway] handle failed");
            }
        }
    }
}
