using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Short_termApartmentAPI.Controllers;

[ApiController]
[Route("api/landlord")]
[Authorize(Roles = "landlord")]
public sealed class LandlordController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { message = "landlord ok" });
}
