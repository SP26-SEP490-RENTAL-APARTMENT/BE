using BLL.Services.Interfaces;
using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Implements
{
    public class AuthService : IAuthService
    {
        public AuthService()
        {
        }

        public Task<LoginResponseDto?> LoginAsync(LoginRequestDto dto)
        {
            throw new NotImplementedException();
        }

        public Task<bool> LogoutAsync(RefreshTokenRequestDto dto)
        {
            throw new NotImplementedException();
        }

        public Task<RefreshTokenResponseDto?> RefreshTokenAsync(RefreshTokenRequestDto dto)
        {
            throw new NotImplementedException();
        }
    }
}
