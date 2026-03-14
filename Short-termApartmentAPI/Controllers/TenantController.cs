using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Short_termApartmentAPI.Controllers;

[ApiController]
[Route("api/tenant")]
[Authorize(Roles = "tenant")]
public sealed class TenantController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { message = "tenant ok" });
}
