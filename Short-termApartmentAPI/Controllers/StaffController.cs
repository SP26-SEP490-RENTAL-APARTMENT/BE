using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Short_termApartmentAPI.Controllers;

[ApiController]
[Route("api/staff")]
[Authorize(Roles = "staff")]
public sealed class StaffController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { message = "staff ok" });
}
