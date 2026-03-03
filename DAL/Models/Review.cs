using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class Review
{
    public Guid ReviewId { get; set; }

    public Guid BookingId { get; set; }

    public Guid ReviewerId { get; set; }

    public Guid ReviewedId { get; set; }

    public Guid? ApartmentId { get; set; }

    public sbyte? Rating { get; set; }

    public string? CommentEn { get; set; }

    public string? CommentVi { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Apartment? Apartment { get; set; }

    public virtual Booking Booking { get; set; } = null!;

    public virtual User Reviewed { get; set; } = null!;

    public virtual User Reviewer { get; set; } = null!;
}
