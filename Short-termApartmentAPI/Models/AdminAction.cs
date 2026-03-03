using System;
using System.Collections.Generic;

namespace Short_termApartmentAPI.Models;

public partial class AdminAction
{
    public Guid ActionId { get; set; }

    public Guid AdminId { get; set; }

    public string ActionType { get; set; } = null!;

    public string TargetType { get; set; } = null!;

    public Guid TargetId { get; set; }

    public string? Description { get; set; }

    public string? PreviousValue { get; set; }

    public string? NewValue { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User Admin { get; set; } = null!;
}
