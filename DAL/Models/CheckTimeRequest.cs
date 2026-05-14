namespace DAL.Models;
public partial class CheckTimeRequest
{
    public Guid Id { get; set; }

    public Guid BookingId { get; set; }

    public string RequestType { get; set; } = null!;

    // Guest's originally requested time (immutable)
    public DateTime RequestedTime { get; set; }

    // Host's counter-offer
    public DateTime? CounterOfferedTime { get; set; }
    public decimal? CounterOfferedFee { get; set; }

    // Final agreed time & fee
    public DateTime? AgreedTime { get; set; }
    public decimal? AgreedFee { get; set; }

    public string Status { get; set; } = "Pending";
    public string? GuestReason { get; set; }
    public string? HostResponse { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public Guid? ProcessedById { get; set; }
    public virtual Booking Booking { get; set; } = null!;
}