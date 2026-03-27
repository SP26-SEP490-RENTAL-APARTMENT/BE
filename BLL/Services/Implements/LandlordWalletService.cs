using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public class LandlordWalletService : ILandlordWalletService
{
    private readonly IRepository<LandlordWallet> _walletRepository;

    public LandlordWalletService(IRepository<LandlordWallet> walletRepository)
    {
        _walletRepository = walletRepository;
    }

    public async Task<LandlordWallet> GetOrCreateAsync(Guid landlordId)
    {
        var existing = (await _walletRepository.FindAsync(w => w.LandlordId == landlordId)).FirstOrDefault();
        if (existing != null)
        {
            return existing;
        }

        var wallet = new LandlordWallet
        {
            LandlordId = landlordId,
            PendingBalance = 0m,
            AvailableBalance = 0m,
            UpdatedAt = DateTime.UtcNow
        };

        await _walletRepository.AddAsync(wallet);
        await _walletRepository.SaveChangesAsync();

        return wallet;
    }

    public async Task CreditPendingAsync(Guid landlordId, decimal amount)
    {
        if (amount <= 0)
        {
            return;
        }

        var wallet = await GetOrCreateAsync(landlordId);
        wallet.PendingBalance += amount;
        wallet.UpdatedAt = DateTime.UtcNow;

        _walletRepository.Update(wallet);
        await _walletRepository.SaveChangesAsync();
    }
}
