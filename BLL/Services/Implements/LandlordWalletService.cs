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

    public async Task ReserveForPayoutAsync(Guid landlordId, long amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero.");
        }

        var wallet = await GetOrCreateAsync(landlordId);
        var value = Convert.ToDecimal(amount);
        if (wallet.AvailableBalance < value)
        {
            throw new InvalidOperationException("Insufficient available balance for payout.");
        }

        wallet.AvailableBalance -= value;
        wallet.UpdatedAt = DateTime.UtcNow;
        _walletRepository.Update(wallet);
        await _walletRepository.SaveChangesAsync();
    }

    public async Task FinalizePayoutSuccessAsync(Guid landlordId, long amount)
    {
        var wallet = await GetOrCreateAsync(landlordId);
        wallet.UpdatedAt = DateTime.UtcNow;
        _walletRepository.Update(wallet);
        await _walletRepository.SaveChangesAsync();
    }

    public async Task RollbackPayoutAsync(Guid landlordId, long amount)
    {
        if (amount <= 0)
        {
            return;
        }

        var wallet = await GetOrCreateAsync(landlordId);
        wallet.AvailableBalance += Convert.ToDecimal(amount);
        wallet.UpdatedAt = DateTime.UtcNow;
        _walletRepository.Update(wallet);
        await _walletRepository.SaveChangesAsync();
    }
}
