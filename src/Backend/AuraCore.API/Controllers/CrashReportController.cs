using AuraCore.API.Application.Interfaces;
using AuraCore.API.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuraCore.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class CrashReportController : ControllerBase
{
    private readonly ICrashReportRepository _crashes;
    public CrashReportController(ICrashReportRepository crashes) => _crashes = crashes;

    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] CrashReportRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.AppVersion) || request.AppVersion.Length > 32)
            return BadRequest(new { error = "Invalid AppVersion" });
        if (request.ExceptionType?.Length > 512)
            return BadRequest(new { error = "ExceptionType too long" });
        if (request.StackTrace?.Length > 50000)
            return BadRequest(new { error = "StackTrace too long (max 50KB)" });
        if (request.SystemInfo?.Length > 10000)
            return BadRequest(new { error = "SystemInfo too long" });

        var report = new CrashReport
        {
            DeviceId = request.DeviceId,
            AppVersion = request.AppVersion,
            ExceptionType = request.ExceptionType,
            StackTrace = request.StackTrace,
            SystemInfo = request.SystemInfo ?? "{}"
        };
        var created = await _crashes.CreateAsync(report, ct);
        return Created($"/api/crashreport/{created.Id}", new { reportId = created.Id });
    }
}

public sealed record CrashReportRequest(
    Guid DeviceId, string AppVersion, string ExceptionType, string StackTrace, string? SystemInfo);
