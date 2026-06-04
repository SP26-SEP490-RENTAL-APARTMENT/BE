using System;

namespace Common.DTOs
{
    public class NotificationDto
    {
        public Guid NotificationId { get; set; }
        public string Type { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string? TitleVi { get; set; }
        public string Message { get; set; } = null!;
        public string? MessageVi { get; set; }
        public Guid? ReferenceId { get; set; }
        public string? ReferenceType { get; set; }
        public bool? IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}