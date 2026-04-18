namespace Common.DTOs;

public class LandlordWalletBalanceDto
{
    public decimal PendingBalance { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal TotalBalance { get; set; }
    public DateTime? UpdatedAt { get; set; }
}