using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class LoginRequestDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [MinLength(6)]
        public required string Password { get; set; }
    }

    public class LoginResponseDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string Role { get; set; }
        public List<string> Roles { get; set; } = new();
    }

    public class PasswordResetRequestDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }
    }

    public class PasswordResetDto
    {
        [Required]
        [MinLength(6)]
        public required string OldPassword { get; set; }

        [Required]
        [MinLength(6)]
        public required string NewPassword { get; set; }

        [Required]
        [MinLength(6)]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        public required string ConfirmNewPassword { get; set; }
    }

    public class RegisterRequestDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [MinLength(6)]
        public required string Password { get; set; }
        
        [Required]
        public required string FullName { get; set; }

        [Required]
        [RegularExpression("^(tenant|landlord)$", ErrorMessage = "Role must be 'tenant' or 'landlord'")]
        public required string Role { get; set; }

        public string? Phone { get; set; }
    }

    public class AddUserRoleRequestDto
    {
        [Required]
        [RegularExpression("^(tenant|landlord)$", ErrorMessage = "Target role must be 'tenant' or 'landlord'")]
        public required string TargetRole { get; set; }
    }

    public class AddUserRoleResponseDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string Role { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
    }
}
