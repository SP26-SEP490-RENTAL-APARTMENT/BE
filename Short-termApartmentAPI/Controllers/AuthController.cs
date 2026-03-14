using BLL.Services.Interfaces;
using Common.DTOs;
using Microsoft.AspNetCore.Mvc;
using Short_termApartmentAPI.Middlewares;

namespace Short_termApartmentAPI.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }
    }
}
