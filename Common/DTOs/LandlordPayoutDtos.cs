using System.ComponentModel.DataAnnotations;

namespace Common.DTOs;

public class UpsertLandlordPayoutProfileRequestDto
{
    [MaxLength(20)]
    public string? MomoWalletPhone { get; set; }

    [MaxLength(150)]
    public string? ReceiverName { get; set; }

    [MaxLength(30)]
    public string? PersonalId { get; set; }

    [MaxLength(40)]
    public string? BankAccountNo { get; set; }

    [MaxLength(40)]
    public string? BankCardNo { get; set; }

    [MaxLength(20)]
    public string? BankCode { get; set; }

    [MaxLength(20)]
    public string? PreferredPayoutMethod { get; set; }
}

public class LandlordPayoutProfileDto
{
    public string? MomoWalletPhone { get; set; }
    public string? ReceiverName { get; set; }
    public string? PersonalIdMasked { get; set; }
    public string? BankAccountNoMasked { get; set; }
    public string? BankCardNoMasked { get; set; }
    public string? BankCode { get; set; }
    public string? PreferredPayoutMethod { get; set; }
}

public class CreateLandlordPayoutRequestDto
{
    [Range(1000, 200000000)]
    public long Amount { get; set; }

    [Required]
    [MaxLength(20)]
    public string Channel { get; set; } = "wallet"; // wallet|bank

    [MaxLength(200)]
    public string? OrderInfo { get; set; }
}

public class LandlordPayoutResponseDto
{
    public Guid PayoutId { get; set; }
    public long Amount { get; set; }
    public long FeeAmount { get; set; }
    public long NetAmount { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string MomoOrderId { get; set; } = string.Empty;
    public string MomoRequestId { get; set; } = string.Empty;
    public string? MomoTransId { get; set; }
    public int? ResultCode { get; set; }
    public string? Message { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
