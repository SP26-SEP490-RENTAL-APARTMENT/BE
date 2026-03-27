using System;

namespace DAL.Models;

public partial class LandlordWallet
{
    public Guid LandlordId { get; set; }

    public decimal PendingBalance { get; set; }

    public decimal AvailableBalance { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Landlord Landlord { get; set; } = null!;
}
