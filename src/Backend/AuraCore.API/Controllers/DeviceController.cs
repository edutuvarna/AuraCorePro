using AuraCore.API.Application.Interfaces;
using AuraCore.API.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuraCore.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class DeviceController : ControllerBase
{
    private readonly IDeviceRepository _devices;
    public DeviceController(IDeviceRepository devices) => _devices = devices;

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest request, CancellationToken ct)
    {
        var existing = await _devices.GetByFingerprintAsync(request.LicenseId, request.HardwareFingerprint, ct);
        if (existing is not null)
        {
            await _devices.UpdateLastSeenAsync(existing.Id, ct);
            return Ok(new { deviceId = existing.Id, status = "already_registered" });
        }

        var device = new Device
        {
            LicenseId = request.LicenseId,
            HardwareFingerprint = request.HardwareFingerprint,
            MachineName = request.MachineName,
            OsVersion = request.OsVersion
        };
        var created = await _devices.RegisterAsync(device, ct);
        return Created($"/api/device/{created.Id}", new { deviceId = created.Id, status = "registered" });
    }
}

public sealed record RegisterDeviceRequest(
    Guid LicenseId, string HardwareFingerprint, string MachineName, string OsVersion);
