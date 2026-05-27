using BLL.Services.Interfaces;
using Common.DTOs;
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
            UpdatedAt = Common.Utils.VietnamTime.Now
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
        wallet.UpdatedAt = Common.Utils.VietnamTime.Now;

        _walletRepository.Update(wallet);
        await _walletRepository.SaveChangesAsync();
    }

    public async Task ReleasePendingToAvailableAsync(Guid landlordId, decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero.");
        }

        var wallet = await GetOrCreateAsync(landlordId);
        if (wallet.PendingBalance < amount)
        {
            throw new InvalidOperationException("Insufficient pending balance.");
        }

        wallet.PendingBalance -= amount;
        wallet.AvailableBalance += amount;
        wallet.UpdatedAt = Common.Utils.VietnamTime.Now;
        _walletRepository.Update(wallet);
        await _walletRepository.SaveChangesAsync();
    }

    public async Task RollbackPendingAsync(Guid landlordId, decimal amount)
    {
        if (amount <= 0)
        {
            return;
        }

        var wallet = await GetOrCreateAsync(landlordId);
        if (wallet.PendingBalance < amount)
        {
            throw new InvalidOperationException("Insufficient pending balance to rollback refund credit.");
        }

        wallet.PendingBalance -= amount;
        wallet.UpdatedAt = Common.Utils.VietnamTime.Now;
        _walletRepository.Update(wallet);
        await _walletRepository.SaveChangesAsync();
    }

    public async Task DebitAvailableAsync(Guid landlordId, decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero.");
        }

        var wallet = await GetOrCreateAsync(landlordId);
        if (wallet.AvailableBalance < amount)
        {
            throw new InvalidOperationException("Insufficient available balance.");
        }

        wallet.AvailableBalance -= amount;
        wallet.UpdatedAt = Common.Utils.VietnamTime.Now;
        _walletRepository.Update(wallet);
        await _walletRepository.SaveChangesAsync();
    }

    public async Task<LandlordPenaltyApplicationResultDto> ApplyOccupiedIncidentPenaltyAsync(Guid landlordId, decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero.");
        }

        var wallet = await GetOrCreateAsync(landlordId);
        var remaining = amount;

        var fromAvailable = Math.Min(wallet.AvailableBalance, remaining);
        wallet.AvailableBalance -= fromAvailable;
        remaining -= fromAvailable;

        var fromPending = Math.Min(wallet.PendingBalance, remaining);
        wallet.PendingBalance -= fromPending;
        remaining -= fromPending;

        // Policy: if wallet funds are insufficient, keep processing and record landlord debt.
        // Debt is represented as a negative available balance.
        if (remaining > 0)
        {
            wallet.AvailableBalance -= remaining;
        }

        wallet.UpdatedAt = Common.Utils.VietnamTime.Now;
        _walletRepository.Update(wallet);
        await _walletRepository.SaveChangesAsync();

        return new LandlordPenaltyApplicationResultDto
        {
            RequestedAmount = amount,
            DeductedFromAvailable = Math.Round(fromAvailable, 2, MidpointRounding.AwayFromZero),
            DeductedFromPending = Math.Round(fromPending, 2, MidpointRounding.AwayFromZero),
            DebtRecorded = Math.Round(remaining, 2, MidpointRounding.AwayFromZero)
        };
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
        wallet.UpdatedAt = Common.Utils.VietnamTime.Now;
        _walletRepository.Update(wallet);
        await _walletRepository.SaveChangesAsync();
    }

    public async Task FinalizePayoutSuccessAsync(Guid landlordId, long amount)
    {
        var wallet = await GetOrCreateAsync(landlordId);
        wallet.UpdatedAt = Common.Utils.VietnamTime.Now;
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
        wallet.UpdatedAt = Common.Utils.VietnamTime.Now;
        _walletRepository.Update(wallet);
        await _walletRepository.SaveChangesAsync();
    }
}
