using Microsoft.AspNetCore.Mvc;

namespace Project_Malawi_Resto.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { status = "ok", application = "Malawi Resto API" });
    }
}
