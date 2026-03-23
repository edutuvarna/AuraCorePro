using Microsoft.AspNetCore.Mvc;
namespace AuraCore.API.Controllers;

[ApiController, Route("api/config")]
public sealed class ConfigController : ControllerBase
{
    [HttpGet("remote")]
    public IActionResult GetRemoteConfig() => Ok(new { flags = new { }, settings = new { } });
}
