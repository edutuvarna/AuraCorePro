using AuraCore.API.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/licenses")]
[Authorize(Roles = "admin")]
public sealed class AdminLicenseController : ControllerBase
{
    private readonly ILicenseService _licenses;
    public AdminLicenseController(ILicenseService licenses) => _licenses = licenses;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLicenseRequest request, CancellationToken ct)
    {
        var license = await _licenses.CreateAsync(request.UserId, request.Tier, request.MaxDevices, ct);
        return Created($"/api/admin/licenses/{license.Id}", new
        {
            license.Id, license.Key, license.Tier, license.MaxDevices
        });
    }
}

public sealed record CreateLicenseRequest(Guid UserId, string Tier, int MaxDevices);
