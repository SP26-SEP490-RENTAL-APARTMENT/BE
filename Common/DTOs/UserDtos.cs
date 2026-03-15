using System;
using System.ComponentModel.DataAnnotations;

namespace Common.DTOs
{
    public class UserDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = null!;
        public string Role { get; set; } = null!; 
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public bool? IdentityVerified { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class CreateUserDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;
        
        [Required]
        public string Password { get; set; } = null!;
        
        [Required]
        [RegularExpression("^(staff|admin)$", ErrorMessage = "Role must be 'staff' or 'admin'")]
        public string Role { get; set; } = null!;
        
        public string? FullName { get; set; }
        
        public string? Phone { get; set; }
        
        public bool? IdentityVerified { get; set; }
    }

    public class UpdateUserDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;
        
        [Required]
        [RegularExpression("^(staff|admin)$", ErrorMessage = "Role must be 'staff' or 'admin'")]
        public string Role { get; set; } = null!;
        
        public string? FullName { get; set; }
        
        public string? Phone { get; set; }
        
        public bool? IdentityVerified { get; set; }
    }
}