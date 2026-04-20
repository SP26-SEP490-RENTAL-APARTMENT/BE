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
        Task<AddUserRoleResponseDto?> AddRoleAsync(Guid userId, AddUserRoleRequestDto dto);
        Task<RefreshTokenResponseDto?> RefreshTokenAsync(RefreshTokenRequestDto dto);
        Task<bool> LogoutAsync(RefreshTokenRequestDto dto);
        Task<ResponseDTO> RequestVerificationAsync(RequestVerificationDto dto);
        Task<ResponseDTO> ResetPasswordAsync(ResetPasswordWithVerificationDto dto);
        Task<ResponseDTO> RequestPasswordResetAsync(PasswordResetRequestDto dto);
        Task<ResponseDTO> ResetPasswordByTokenAsync(string token, ResetPasswordByTokenDto dto);
        Task<ResponseDTO> ChangePasswordAsync(Guid userId, PasswordResetDto dto);
    }
}
