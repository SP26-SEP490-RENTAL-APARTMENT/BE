using DAL.Models;

namespace BLL.Services.Interfaces;

public interface ILandlordWalletService
{
    Task<LandlordWallet> GetOrCreateAsync(Guid landlordId);

    Task CreditPendingAsync(Guid landlordId, decimal amount);
}
