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
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Implements
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IRepository<Tenant> _tenantRepository;
        private readonly IRepository<Landlord> _landlordRepository;
        private readonly JwtSettings _jwtSettings;

        public AuthService(
            IUserRepository userRepository,
            IRepository<Tenant> tenantRepository,
            IRepository<Landlord> landlordRepository,
            IOptions<JwtSettings> jwtSettings)
        {
            _userRepository = userRepository;
            _tenantRepository = tenantRepository;
            _landlordRepository = landlordRepository;
            _jwtSettings = jwtSettings.Value;
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

            return new LoginResponseDto
            {
                Id = user.UserId,
                FullName = user.FullName ?? string.Empty,
                Role = user.Role,
                Email = user.Email,
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
                CreatedAt = DateTime.UtcNow
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
                Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
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
    }
}