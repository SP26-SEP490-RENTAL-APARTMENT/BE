using BLL.Services.Interfaces;
using Common.DTOs;
using Common.Settings;
using Common.Utils;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Implements
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly JwtSettings _jwtSettings;
        private readonly EmailService _emailService;

        public AuthService(IUserRepository userRepository, IOptions<JwtSettings> jwtSettings, EmailService emailService)
        {
            _userRepository = userRepository;
            _jwtSettings = jwtSettings.Value;
            _emailService = emailService;
        }

        public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto dto)
        {
            var users = await _userRepository.FindAsync(u => u.Email == dto.Email);
            var user = users.FirstOrDefault();

            if (user == null || !PasswordHasher.VerifyPassword(dto.Password, user.PasswordHash))
            {
                return null;
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings.Key);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.Role)
                }),
                Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            return new LoginResponseDto
            {
                Id = user.UserId,
                FullName = user.FullName ?? string.Empty,
                Role = user.Role,
                Email = user.Email,
                AccessToken = tokenString,
                RefreshToken = string.Empty, // Placeholder for actual refresh token logic
                IsActive = true
            };
        }

        public async Task<LoginResponseDto?> RegisterAsync(RegisterRequestDto dto)
        {
            var existingUsers = await _userRepository.FindAsync(u => u.Email == dto.Email);
            if (existingUsers.Any())
            {
                return null;
            }

            var passwordHash = PasswordHasher.HashPassword(dto.Password);

            var newUser = new User
            {
                Email = dto.Email,
                PasswordHash = passwordHash,
                FullName = dto.FullName,
                Phone = dto.Phone,
                Role = dto.Role,
                CreatedAt = DateTime.UtcNow
            };

            await _userRepository.AddAsync(newUser);
            await _userRepository.SaveChangesAsync();


            return await LoginAsync(new LoginRequestDto { Email = dto.Email, Password = dto.Password });
        }


        private string GenerateSecureToken()
        {
            // This generates a 256-bit token
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            // Convert to URL-safe Base64 string
            return Convert.ToBase64String(bytes)
                          .Replace('+', '-')
                          .Replace('/', '_')
                          .TrimEnd('='); // Remove padding '=' characters for cleaner URLs
        }

        public Task<RefreshTokenResponseDto?> RefreshTokenAsync(RefreshTokenRequestDto dto)
        {
            throw new NotImplementedException();
        }

        public Task<bool> LogoutAsync(RefreshTokenRequestDto dto)
        {
            throw new NotImplementedException();
        }

        public async Task<ResponseDTO> RequestPasswordResetAsync(PasswordResetRequestDto dto)
        {
            var userList = await _userRepository.FindAsync(u => u.Email == dto.Email);
            var user = userList.FirstOrDefault();
            if (user == null) return new ResponseDTO { Success = false, Message = "Email not found." };

            user.Token = GenerateSecureToken();
            user.TokenExpired = DateTime.UtcNow.AddHours(1); // Token valid for 1 hour
            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();

            var resetLink = $"https://doigiumcaiurlnha.com/reset-password?token={user.Token}";
            var body = $"Please reset your password by clicking here: <a href='{resetLink}'>Reset Password</a>";
            await _emailService.SendEmailAsync(user.Email, "Password Reset Request", body);

            return new ResponseDTO { Success = true, Message = "Password reset request successful." };
        }

        public async Task<ResponseDTO> ResetPasswordAsync(string token, PasswordResetDto dto)
        {
            var userList = await _userRepository.FindAsync(u => u.Token == token);
            var user = userList.FirstOrDefault();

            if (user == null || user.TokenExpired == null || user.TokenExpired < DateTime.UtcNow)
                return new ResponseDTO { Success = false, Message = "Invalid or expired token." };

            if (dto.NewPassword != dto.ConfirmNewPassword)
                return new ResponseDTO { Success = false, Message = "New passwords do not match." };

            user.PasswordHash = PasswordHasher.HashPassword(dto.NewPassword);
            user.Token = null;
            user.TokenExpired = null;

            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();

            return new ResponseDTO { Success = true, Message = "Password reset successful." };
        }

        public async Task<ResponseDTO> ChangePasswordAsync(Guid userId, PasswordResetDto dto)
        {
            var userList = await _userRepository.FindAsync(u => u.UserId == userId);
            var user = userList.FirstOrDefault();
            if (user == null)
                return new ResponseDTO { Success = false, Message = "User not found." };
            if (!PasswordHasher.VerifyPassword(dto.OldPassword, user.PasswordHash))
                return new ResponseDTO { Success = false, Message = "Incorrect old password." };
            if (dto.NewPassword != dto.ConfirmNewPassword)
                return new ResponseDTO { Success = false, Message = "New passwords do not match." };
            user.PasswordHash = PasswordHasher.HashPassword(dto.NewPassword);
            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();
            return new ResponseDTO { Success = true, Message = "Password changed successfully." };
        }
    }
    
}