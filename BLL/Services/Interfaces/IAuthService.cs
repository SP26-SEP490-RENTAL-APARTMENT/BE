using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResponseDto?> LoginAsync(LoginRequestDto dto);
        Task<LoginResponseDto?> RegisterAsync(RegisterRequestDto dto);
        Task<RefreshTokenResponseDto?> RefreshTokenAsync(RefreshTokenRequestDto dto);
        Task<bool> LogoutAsync(RefreshTokenRequestDto dto);
        Task<ResponseDTO> RequestPasswordResetAsync(PasswordResetRequestDto dto);
        Task<ResponseDTO> ResetPasswordAsync(string token, PasswordResetDto dto);
        Task<ResponseDTO> ChangePasswordAsync(Guid userId, PasswordResetDto dto);
    }
}
