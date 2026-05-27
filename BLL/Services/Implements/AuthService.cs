using BLL.Services.Interfaces;
using Common.DTOs;
using Common.Settings;
using Common.Utils;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Concurrent;
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
        private readonly IRepository<Tenant> _tenantRepository;
        private readonly IRepository<Landlord> _landlordRepository;
        private readonly IRepository<Apartment> _apartmentRepository;
        private readonly JwtSettings _jwtSettings;
        private readonly FrontendSettings _frontendSettings;
        private readonly EmailService _emailService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private static readonly ConcurrentDictionary<string, List<DateTime>> VerificationRequestAttempts = new();
        private static readonly ConcurrentDictionary<string, List<DateTime>> VerificationResetAttempts = new();
        private const int VerificationCodeLength = 6;
        private const int VerificationCodeExpiryMinutes = 10;
        private const int RequestVerificationMaxAttempts = 3;
        private const int RequestVerificationWindowMinutes = 15;
        private const int ResetPasswordMaxAttempts = 5;
        private const int ResetPasswordWindowMinutes = 15;
        private const string GenericInvalidVerificationMessage = "The verification code provided is invalid. Please try again or request a new code.";

        public AuthService(
            IUserRepository userRepository, 
            IRepository<Tenant> tenantRepository, 
            IRepository<Landlord> landlordRepository,
            IRepository<Apartment> apartmentRepository,
            IOptions<JwtSettings> jwtSettings,
            IOptions<FrontendSettings> frontendSettings,
            EmailService emailService,
            IHttpContextAccessor httpContextAccessor)
        {
            _userRepository = userRepository;
            _tenantRepository = tenantRepository;
            _landlordRepository = landlordRepository;
            _apartmentRepository = apartmentRepository;
            _jwtSettings = jwtSettings.Value;
            _frontendSettings = frontendSettings.Value;
            _emailService = emailService;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto dto)
        {
            var users = await _userRepository.FindAsync(u => u.Email == dto.Email);
            var user = users.FirstOrDefault();

            if (user == null || !PasswordHasher.VerifyPassword(dto.Password, user.PasswordHash))
            {
                return null;
            }

            var roles = await GetResolvedRolesAsync(user);
            var tokenString = CreateAccessToken(user, roles);
            Guid? subscriptionPlanId = null;

            if (roles.Any(r => string.Equals(r, "landlord", StringComparison.OrdinalIgnoreCase)))
            {
                var landlord = (await _landlordRepository.FindAsync(l => l.LandlordId == user.UserId)).FirstOrDefault();
                subscriptionPlanId = landlord?.CurrentPlanId;
            }

            return new LoginResponseDto
            {
                Id = user.UserId,
                FullName = user.FullName ?? string.Empty,
                Role = user.Role,
                Email = user.Email,
                SubscriptionPlanId = subscriptionPlanId,
                AccessToken = tokenString,
                RefreshToken = string.Empty, // Placeholder for actual refresh token logic
                IsActive = true,
                Roles = roles
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
                CreatedAt = Common.Utils.VietnamTime.Now
            };

            await _userRepository.AddAsync(newUser);
            await _userRepository.SaveChangesAsync();

            await CreateProfileIfNeededAsync(newUser.UserId, dto.Role);


            return await LoginAsync(new LoginRequestDto { Email = dto.Email, Password = dto.Password });
        }

        public async Task<AddUserRoleResponseDto?> AddRoleAsync(Guid userId, AddUserRoleRequestDto dto)
        {
            var users = await _userRepository.FindAsync(u => u.UserId == userId);
            var user = users.FirstOrDefault();
            if (user == null)
            {
                return null;
            }

            if (!IsSupportedMemberRole(dto.TargetRole))
            {
                throw new ArgumentException("Target role must be tenant or landlord.");
            }

            if (!IsSupportedMemberRole(user.Role))
            {
                throw new InvalidOperationException("Only tenant or landlord accounts can upgrade roles.");
            }

            await CreateProfileIfNeededAsync(userId, dto.TargetRole);

            var roles = await GetResolvedRolesAsync(user);
            var tokenString = CreateAccessToken(user, roles);

            return new AddUserRoleResponseDto
            {
                Id = user.UserId,
                FullName = user.FullName ?? string.Empty,
                Email = user.Email,
                AccessToken = tokenString,
                RefreshToken = string.Empty,
                IsActive = true,
                Role = user.Role,
                Roles = roles
            };
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

        private string GenerateVerificationCode()
        {
            var code = RandomNumberGenerator.GetInt32(0, 1000000);
            return code.ToString($"D{VerificationCodeLength}");
        }

        private static bool IsEmailIdentifier(string identifier)
        {
            return identifier.Contains('@', StringComparison.Ordinal);
        }

        private static string NormalizeIdentifier(string identifier)
        {
            return identifier.Trim();
        }

        private string GetClientIpAddress()
        {
            return _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown-ip";
        }

        private static bool IsStrongPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            {
                return false;
            }

            var hasUpper = password.Any(char.IsUpper);
            var hasLower = password.Any(char.IsLower);
            var hasDigit = password.Any(char.IsDigit);
            var hasSpecial = password.Any(ch => !char.IsLetterOrDigit(ch));

            return hasUpper && hasLower && hasDigit && hasSpecial;
        }

        private static bool IsRateLimited(ConcurrentDictionary<string, List<DateTime>> bucket, string key, int maxAttempts, int windowMinutes)
        {
            var now = VietnamTime.Now;
            var windowStart = now.AddMinutes(-windowMinutes);

            var attempts = bucket.GetOrAdd(key, _ => new List<DateTime>());
            lock (attempts)
            {
                attempts.RemoveAll(ts => ts < windowStart);
                if (attempts.Count >= maxAttempts)
                {
                    return true;
                }

                attempts.Add(now);
                return false;
            }
        }

        public Task<RefreshTokenResponseDto?> RefreshTokenAsync(RefreshTokenRequestDto dto)
        {
            throw new NotImplementedException();
        }

        public Task<bool> LogoutAsync(RefreshTokenRequestDto dto)
        {
            throw new NotImplementedException();
        }

        private async Task<List<string>> GetResolvedRolesAsync(User user)
        {
            var roles = new List<string>();

            if (IsSupportedMemberRole(user.Role))
            {
                roles.Add(user.Role);
            }
            else
            {
                roles.Add(user.Role);
            }

            var tenantExists = (await _tenantRepository.FindAsync(t => t.TenantId == user.UserId)).Any();
            if (tenantExists && !roles.Any(r => string.Equals(r, "tenant", StringComparison.OrdinalIgnoreCase)))
            {
                roles.Add("tenant");
            }

            var landlordExists = (await _landlordRepository.FindAsync(l => l.LandlordId == user.UserId)).Any();
            if (landlordExists && !roles.Any(r => string.Equals(r, "landlord", StringComparison.OrdinalIgnoreCase)))
            {
                roles.Add("landlord");
            }

            if (string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase) && !roles.Any(r => string.Equals(r, "admin", StringComparison.OrdinalIgnoreCase)))
            {
                roles.Add("admin");
            }

            if (string.Equals(user.Role, "staff", StringComparison.OrdinalIgnoreCase) && !roles.Any(r => string.Equals(r, "staff", StringComparison.OrdinalIgnoreCase)))
            {
                roles.Add("staff");
            }

            return roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private string CreateAccessToken(User user, IEnumerable<string> roles)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings.Key);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email)
            };

            claims.AddRange(roles.Distinct(StringComparer.OrdinalIgnoreCase).Select(role => new Claim(ClaimTypes.Role, role)));

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = Common.Utils.VietnamTime.Now.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private async Task CreateProfileIfNeededAsync(Guid userId, string role)
        {
            if (string.Equals(role, "tenant", StringComparison.OrdinalIgnoreCase))
            {
                var exists = (await _tenantRepository.FindAsync(t => t.TenantId == userId)).Any();
                if (!exists)
                {
                    await _tenantRepository.AddAsync(new Tenant { TenantId = userId });
                    await _tenantRepository.SaveChangesAsync();
                }
            }

            if (string.Equals(role, "landlord", StringComparison.OrdinalIgnoreCase))
            {
                var exists = (await _landlordRepository.FindAsync(l => l.LandlordId == userId)).Any();
                if (!exists)
                {
                    await _landlordRepository.AddAsync(new Landlord { LandlordId = userId });
                    await _landlordRepository.SaveChangesAsync();
                }
            }
        }

        private static bool IsSupportedMemberRole(string role)
        {
            return string.Equals(role, "tenant", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "landlord", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<ResponseDTO> RequestVerificationAsync(RequestVerificationDto dto)
        {
            var identifier = NormalizeIdentifier(dto.EmailAddress);
            var ipAddress = GetClientIpAddress();
            var throttleKey = $"request:{identifier.ToLowerInvariant()}:{ipAddress}";

            if (IsRateLimited(VerificationRequestAttempts, throttleKey, RequestVerificationMaxAttempts, RequestVerificationWindowMinutes))
            {
                return new ResponseDTO
                {
                    Success = false,
                    Message = "Too many verification requests. Please try again later."
                };
            }

            var users = IsEmailIdentifier(identifier)
                ? await _userRepository.FindAsync(u => u.Email == identifier)
                : await _userRepository.FindAsync(u => u.Phone == identifier);

            var user = users.FirstOrDefault();
            if (user == null)
            {
                return new ResponseDTO
                {
                    Success = true,
                    Message = "A code has been sent to your device if the account exists."
                };
            }

            var verificationCode = GenerateVerificationCode();
            user.Token = verificationCode;
            user.TokenExpired = VietnamTime.Now.AddMinutes(VerificationCodeExpiryMinutes);
            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                var body = $"Your verification code is <b>{verificationCode}</b>. It expires in {VerificationCodeExpiryMinutes} minutes.";
                await _emailService.SendEmailAsync(user.Email, "Password Reset Verification Code", body);
            }
            

            return new ResponseDTO
            {
                Success = true,
                Message = "A code has been sent to your device."
            };
        }

        public async Task<ResponseDTO> ResetPasswordAsync(ResetPasswordWithVerificationDto dto)
        {
            var identifier = NormalizeIdentifier(dto.EmailAddress);
            var ipAddress = GetClientIpAddress();
            var throttleKey = $"reset:{identifier.ToLowerInvariant()}:{ipAddress}";

            if (IsRateLimited(VerificationResetAttempts, throttleKey, ResetPasswordMaxAttempts, ResetPasswordWindowMinutes))
            {
                return new ResponseDTO
                {
                    Success = false,
                    Message = "Too many reset attempts. Please try again later."
                };
            }

            var users = IsEmailIdentifier(identifier)
                ? await _userRepository.FindAsync(u => u.Email == identifier)
                : await _userRepository.FindAsync(u => u.Phone == identifier);

            var user = users.FirstOrDefault();
            if (user == null)
            {
                return new ResponseDTO { Success = false, Message = GenericInvalidVerificationMessage };
            }

            if (string.IsNullOrWhiteSpace(user.Token)
                || user.TokenExpired == null
                || user.TokenExpired < VietnamTime.Now
                || !string.Equals(user.Token, dto.VerificationCode?.Trim(), StringComparison.Ordinal))
            {
                return new ResponseDTO { Success = false, Message = GenericInvalidVerificationMessage };
            }

            if (dto.NewPassword != dto.ConfirmNewPassword || !IsStrongPassword(dto.NewPassword))
            {
                return new ResponseDTO
                {
                    Success = false,
                    Message = "Password must be at least 6 characters and include at least 1 uppercase letter, 1 lowercase letter, 1 number, and 1 special character."
                };
            }

            user.PasswordHash = PasswordHasher.HashPassword(dto.NewPassword);
            user.Token = null;
            user.TokenExpired = null;

            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();

            return new ResponseDTO { Success = true, Message = "Password reset successful." };
        }

        public async Task<ResponseDTO> RequestPasswordResetAsync(PasswordResetRequestDto dto)
        {
            var userList = await _userRepository.FindAsync(u => u.Email == dto.Email);
            var user = userList.FirstOrDefault();
            if (user == null) return new ResponseDTO { Success = false, Message = "Email not found." };

            user.Token = GenerateSecureToken();
            user.TokenExpired = Common.Utils.VietnamTime.Now.AddHours(1); // Token valid for 1 hour
            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();

            var baseResetUrl = _frontendSettings.ResetPasswordPageUrl;
            if (string.IsNullOrWhiteSpace(baseResetUrl))
            {
                baseResetUrl = "https://aspdeo123.runasp.net/reset-password";
            }

            var resetLink = $"{baseResetUrl.TrimEnd('/')}?token={Uri.EscapeDataString(user.Token)}";
            var body = $"Please reset your password by clicking here: <a href='{resetLink}'>Reset Password</a>";
            await _emailService.SendEmailAsync(user.Email, "Password Reset Request", body);

            return new ResponseDTO { Success = true, Message = "Password reset request successful." };
        }

        public async Task<ResponseDTO> ResetPasswordByTokenAsync(string token, ResetPasswordByTokenDto dto)
        {
            var userList = await _userRepository.FindAsync(u => u.Token == token);
            var user = userList.FirstOrDefault();

            if (user == null || user.TokenExpired == null || user.TokenExpired < Common.Utils.VietnamTime.Now)
                return new ResponseDTO { Success = false, Message = GenericInvalidVerificationMessage };

            if (dto.NewPassword != dto.ConfirmNewPassword)
                return new ResponseDTO { Success = false, Message = "New passwords do not match." };

            if (!IsStrongPassword(dto.NewPassword))
            {
                return new ResponseDTO
                {
                    Success = false,
                    Message = "Password must be at least 6 characters and include at least 1 uppercase letter, 1 lowercase letter, 1 number, and 1 special character."
                };
            }

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

            if (!IsStrongPassword(dto.NewPassword))
            {
                return new ResponseDTO
                {
                    Success = false,
                    Message = "Password must be at least 6 characters and include at least 1 uppercase letter, 1 lowercase letter, 1 number, and 1 special character."
                };
            }

            user.PasswordHash = PasswordHasher.HashPassword(dto.NewPassword);
            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();
            return new ResponseDTO { Success = true, Message = "Password changed successfully." };
        }

        public async Task<bool> IsUserOwnerOrManager(Guid userId, Guid apartmentId)
        {
            var apartment = await _apartmentRepository.GetByIdAsync(apartmentId)
            ?? throw new ArgumentException("Apartment not found.");

            if (apartment.LandlordId == userId)
                return true;

            return false;
        }
    }
    
}