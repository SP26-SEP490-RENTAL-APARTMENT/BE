using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Short_termApartmentAPI.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]
public sealed class AdminController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { message = "admin ok" });
}
