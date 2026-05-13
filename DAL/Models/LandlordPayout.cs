namespace DAL.Models;

public partial class LandlordPayout
{
    public Guid PayoutId { get; set; }

    public Guid LandlordId { get; set; }

    public long Amount { get; set; }

    public long FeeAmount { get; set; }

    public long NetAmount { get; set; }

    public string? Channel { get; set; } 

    public string? Status { get; set; } 

    public string? MomoOrderId { get; set; } 

    public string? MomoRequestId { get; set; } 

    public string? MomoTransId { get; set; }

    // Provider-neutral fields for multi-provider support
    public string? ProviderName { get; set; }
    public string? ProviderPayoutId { get; set; }
    public string? ProviderRequestId { get; set; }
    public string? ProviderTransId { get; set; }
    public string? ProviderRequestBody { get; set; }
    public string? ProviderResponseBody { get; set; }

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
