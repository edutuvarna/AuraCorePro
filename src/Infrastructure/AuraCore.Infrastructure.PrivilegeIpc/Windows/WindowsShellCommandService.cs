using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.IPC.Contracts;
using Microsoft.Extensions.Logging;

namespace AuraCore.Infrastructure.PrivilegeIpc.Windows;

public sealed class WindowsShellCommandService : IShellCommandService
{
    private readonly ILogger<WindowsShellCommandService> _logger;
    private const string PipeName = "AuraCorePro";
    private const int ConnectTimeoutMs = 2000;
    private const int MaxResponseBytes = 1 * 1024 * 1024; // 1 MB cap

    public WindowsShellCommandService(ILogger<WindowsShellCommandService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ShellResult> RunPrivilegedAsync(PrivilegedCommand command, CancellationToken ct = default)
    {
        _logger.LogInformation("[windows-ipc] dispatching action={ActionId}", command.Id);

        var correlationId = Guid.NewGuid().ToString("N");
        var request = BuildRequest(command, correlationId);
        if (request is null)
        {
            _logger.LogWarning("[windows-ipc] unknown action id {ActionId}", command.Id);
            return new ShellResult(false, -1, "", $"unknown action {command.Id}", PrivilegeAuthResult.Denied);
        }

        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(ConnectTimeoutMs, ct);

            // Write length-prefixed JSON (4-byte LE length + payload), matching PipeServer wire protocol.
            var payload = JsonSerializer.SerializeToUtf8Bytes(request, request.GetType());
            await client.WriteAsync(BitConverter.GetBytes(payload.Length), ct);
            await client.WriteAsync(payload, ct);
            await client.FlushAsync(ct);

            // Read length-prefixed response.
            var lenBytes = new byte[4];
            await client.ReadExactlyAsync(lenBytes, ct);
            var len = BitConverter.ToInt32(lenBytes);
            if (len <= 0 || len > MaxResponseBytes)
            {
                return new ShellResult(false, -4, "", $"server returned invalid response length {len}", PrivilegeAuthResult.Denied);
            }
            var buf = new byte[len];
            await client.ReadExactlyAsync(buf, ct);

            using var doc = JsonDocument.Parse(buf);
            var root = doc.RootElement;

            var success = root.TryGetProperty("Success", out var s) && s.GetBoolean();
            var output  = root.TryGetProperty("Output",  out var o) ? (o.GetString() ?? "") : "";
            var error   = root.TryGetProperty("Error",   out var e) ? e.GetString() : null;
            var exit    = root.TryGetProperty("ExitCode", out var ec) ? ec.GetInt32() : (success ? 0 : -1);

            return new ShellResult(
                Success:    success,
                ExitCode:   exit,
                Stdout:     output,
                Stderr:     error ?? "",
                AuthResult: success ? PrivilegeAuthResult.AlreadyAuthorized : PrivilegeAuthResult.Denied);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("[windows-ipc] pipe connect timeout for {ActionId}", command.Id);
            return new ShellResult(false, -3, "", "privileged helper not responding", PrivilegeAuthResult.HelperMissing);
        }
        catch (FileNotFoundException)
        {
            return new ShellResult(false, -3, "", "privileged helper not installed", PrivilegeAuthResult.HelperMissing);
        }
        catch (IOException ex) when (ex.Message.IndexOf("pipe", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return new ShellResult(false, -3, "", "privileged helper not installed", PrivilegeAuthResult.HelperMissing);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[windows-ipc] dispatch failed for {ActionId}", command.Id);
            return new ShellResult(false, -1, "", ex.Message, PrivilegeAuthResult.Denied);
        }
    }

    private static IpcRequest? BuildRequest(PrivilegedCommand cmd, string correlationId)
    {
        return cmd.Id switch
        {
            "driver.scan"                 => new DriverScanRequest(correlationId),
            "driver.export"               => new DriverExportRequest(correlationId, cmd.Arguments.Length > 0 ? cmd.Arguments[0] : ""),

            "defender.update-signatures"  => new DefenderActionRequest(correlationId, DefenderAction.UpdateSignatures),
            "defender.scan-quick"         => new DefenderActionRequest(correlationId, DefenderAction.QuickScan),
            "defender.scan-full"          => new DefenderActionRequest(correlationId, DefenderAction.FullScan),
            "defender.set-realtime"       => new DefenderActionRequest(correlationId,
                (cmd.Arguments.Length > 0 && cmd.Arguments[0] == "enable")
                    ? DefenderAction.SetRealtimeEnabled
                    : DefenderAction.SetRealtimeDisabled),
            "defender.add-exclusion"      => new DefenderActionRequest(correlationId, DefenderAction.AddExclusion,    cmd.Arguments.ElementAtOrDefault(0)),
            "defender.remove-exclusion"   => new DefenderActionRequest(correlationId, DefenderAction.RemoveExclusion, cmd.Arguments.ElementAtOrDefault(0)),
            "defender.remove-threat"      => new DefenderActionRequest(correlationId, DefenderAction.RemoveThreat,    cmd.Arguments.ElementAtOrDefault(0)),

            "service.start"               => new ServiceControlRequest(correlationId, cmd.Arguments.ElementAtOrDefault(0) ?? "", ServiceAction.Start),
            "service.stop"                => new ServiceControlRequest(correlationId, cmd.Arguments.ElementAtOrDefault(0) ?? "", ServiceAction.Stop),
            "service.restart"             => new ServiceControlRequest(correlationId, cmd.Arguments.ElementAtOrDefault(0) ?? "", ServiceAction.Restart),
            "service.set-startup"         => new ServiceControlRequest(correlationId,
                cmd.Arguments.ElementAtOrDefault(0) ?? "",
                (cmd.Arguments.ElementAtOrDefault(1) ?? "auto") switch
                {
                    "auto"     => ServiceAction.SetStartupAutomatic,
                    "demand"   => ServiceAction.SetStartupManual,
                    "disabled" => ServiceAction.SetStartupDisabled,
                    _          => ServiceAction.SetStartupAutomatic
                }),

            _ => null
        };
    }
}
