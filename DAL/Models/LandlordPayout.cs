namespace DAL.Models;

public partial class LandlordPayout
{
    public Guid PayoutId { get; set; }

    public Guid LandlordId { get; set; }

    public long Amount { get; set; }

    public long FeeAmount { get; set; }

    public long NetAmount { get; set; }

    public string Channel { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string MomoOrderId { get; set; } = null!;

    public string MomoRequestId { get; set; } = null!;

    public string? MomoTransId { get; set; }

    public int? ResultCode { get; set; }

    public string? Message { get; set; }

    public string? RequestBody { get; set; }

    public string? ResponseBody { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? FailedAt { get; set; }

    public virtual Landlord Landlord { get; set; } = null!;
}
