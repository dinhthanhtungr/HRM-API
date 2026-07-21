using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Domain.Entities.Controllers;

[ApiController]
[Route("api/v1/health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "ok",
            service = "HRM.Api",
            time = DateTime.Now
        });
    }
}
