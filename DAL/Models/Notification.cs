using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class Notification
{
    public Guid NotificationId { get; set; }

    public Guid UserId { get; set; }

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

    public virtual User User { get; set; } = null!;
}
