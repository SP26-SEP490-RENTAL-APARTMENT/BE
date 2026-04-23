using DAL.Models;
using Common.DTOs;

namespace BLL.Services.Interfaces;

public interface ILandlordWalletService
{
    Task<LandlordWallet> GetOrCreateAsync(Guid landlordId);

    Task CreditPendingAsync(Guid landlordId, decimal amount);

    Task RollbackPendingAsync(Guid landlordId, decimal amount);

    Task DebitAvailableAsync(Guid landlordId, decimal amount);

    Task<LandlordPenaltyApplicationResultDto> ApplyOccupiedIncidentPenaltyAsync(Guid landlordId, decimal amount);

    Task ReserveForPayoutAsync(Guid landlordId, long amount);

    Task FinalizePayoutSuccessAsync(Guid landlordId, long amount);

    Task RollbackPayoutAsync(Guid landlordId, long amount);
}
