// src/Backend/AuraCore.API/Controllers/Superadmin/RateLimitConfigController.cs
using AuraCore.API.Application.Services.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuraCore.API.Controllers.Superadmin;

[ApiController]
[Route("api/superadmin/rate-limits")]
[Authorize(Roles = "superadmin")]
public sealed class RateLimitConfigController : ControllerBase
{
    private readonly IRateLimitConfigService _svc;
    public RateLimitConfigController(IRateLimitConfigService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var map = await _svc.GetAllAsync(ct);
        return Ok(new { items = map.Select(kv => new { endpoint = kv.Key, requests = kv.Value.Requests, windowSeconds = kv.Value.WindowSeconds }) });
    }

    [HttpPut("{endpoint}")]
    [AuraCore.API.Filters.AuditAction("UpdateRateLimitPolicy", "SystemSetting")]
    public async Task<IActionResult> Update(string endpoint, [FromBody] RateLimitPolicy policy, CancellationToken ct)
    {
        if (policy.Requests <= 0 || policy.WindowSeconds <= 0)
            return BadRequest(new { error = "invalid_policy" });
        await _svc.UpdateAsync(endpoint, policy, ct);
        return Ok(new { endpoint, policy.Requests, policy.WindowSeconds });
    }
}
