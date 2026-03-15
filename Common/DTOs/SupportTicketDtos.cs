using System;
using System.ComponentModel.DataAnnotations;

namespace Common.DTOs
{
    public class SupportTicketDto
    {
        public Guid TicketId { get; set; }
        public Guid UserId { get; set; }
        public string Subject { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string Category { get; set; } = null!;
        public string? Priority { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public Guid? ResolvedBy { get; set; }
        public string? ResolutionNotes { get; set; }
    }

    public class CreateSupportTicketDto
    {
        [Required]
        [StringLength(255)]
        public string Subject { get; set; } = null!;
        
        [Required]
        public string Description { get; set; } = null!;
        
        [Required]
        public string Category { get; set; } = null!;
        
        public string? Priority { get; set; }
    }

    public class UpdateSupportTicketDto
    {
        [Required]
        [StringLength(255)]
        public string Subject { get; set; } = null!;
        
        [Required]
        public string Description { get; set; } = null!;
        
        [Required]
        public string Category { get; set; } = null!;
        
        public string? Priority { get; set; }
        
        public string? Status { get; set; }
        
        public string? ResolutionNotes { get; set; }
        
        public Guid? ResolvedBy { get; set; }
        
        public DateTime? ResolvedAt { get; set; }
    }
}