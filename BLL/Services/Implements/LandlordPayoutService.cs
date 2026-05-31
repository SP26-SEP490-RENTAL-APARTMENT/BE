using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.Extensions.Logging;

namespace BLL.Services.Implements;

public class LandlordPayoutService : ILandlordPayoutService
{
    private const long MinPayoutAmount = 2000;
    private static readonly HashSet<int> PendingCodes = [7000, 7002];

    private readonly IRepository<LandlordPayout> _payoutRepository;
    private readonly IRepository<Landlord> _landlordRepository;
    private readonly IMomoService _momoService;
    private readonly ILandlordWalletService _walletService;
    private readonly IMomoTransactionService _momoTransactionService;
    private readonly IPayOSPayoutService _payOSService;
    private readonly ILogger<LandlordPayoutService>? ILogger;
    private const int MaxDailyPayoutRequests = 5;

    public LandlordPayoutService(
        IRepository<LandlordPayout> payoutRepository,
        IRepository<Landlord> landlordRepository,
        IMomoService momoService,
        ILandlordWalletService walletService,
        IMomoTransactionService momoTransactionService,
        IPayOSPayoutService payOSService,
        ILogger<LandlordPayoutService>? logger = null)
    {
        _payoutRepository = payoutRepository;
        _landlordRepository = landlordRepository;
        _momoService = momoService;
        _walletService = walletService;
        _momoTransactionService = momoTransactionService;
        _payOSService = payOSService;
        ILogger = logger;
    }

    public async Task<LandlordPayoutResponseDto> CreatePayoutAsync(Guid landlordId, CreateLandlordPayoutRequestDto request, CancellationToken cancellationToken = default)
    {
        var landlord = await _landlordRepository.GetByIdAsync(landlordId)
            ?? throw new ArgumentException("Landlord profile not found.");

        if (request.Amount < MinPayoutAmount || request.Amount > 200000000)
            throw new ArgumentException($"Amount must be between {MinPayoutAmount} and 200,000,000.");

        if (string.IsNullOrWhiteSpace(request.ToBin))
            throw new ArgumentException("ToBin (bank code) is required.");

        if (string.IsNullOrWhiteSpace(request.ToAccountNumber))
            throw new ArgumentException("ToAccountNumber is required.");

        await EnsureDailyPayoutLimitAsync(landlordId);
        await _walletService.ReserveForPayoutAsync(landlordId, request.Amount);

        var walletRolledBack = false;

        async Task RollbackWalletAsync()
        {
            if (walletRolledBack)
            {
                return;
            }

            walletRolledBack = true;
            await _walletService.RollbackPayoutAsync(landlordId, request.Amount);
        }

        try
        {
            // Use PayOS for all payouts (uses request-provided destination)
            var reference = Guid.NewGuid().ToString();
            var accountName = landlord.PayoutReceiverName ?? "Payout Recipient";

            PayOSPayoutResult? payosResult;
            try
            {
                payosResult = await _payOSService.CreateBankPayoutAsync(
                    accountName,
                    request.ToAccountNumber,
                    request.ToBin,
                    request.Amount,
                    reference,
                    cancellationToken);
            }
            catch (InvalidOperationException)
            {
                await RollbackWalletAsync();
                throw;
            }
            catch (Exception ex)
            {
                // Provider failed - rollback wallet immediately
                await RollbackWalletAsync();
                throw new InvalidOperationException("Payout provider request failed. Wallet has been restored.", ex);
            }

            if (payosResult == null)
            {
                await RollbackWalletAsync();
                throw new InvalidOperationException("Payout provider returned null result.");
            }

            var payout = new LandlordPayout
            {
                PayoutId = Guid.NewGuid(),
                LandlordId = landlordId,
                Amount = request.Amount,
                Channel = "bank",
                Status = MapStatus(payosResult.ResultCode),
                MomoOrderId = payosResult.PayoutId ?? string.Empty,
                MomoRequestId = string.Empty,
                MomoTransId = payosResult.TransId,
                ResultCode = payosResult.ResultCode,
                Message = payosResult.Message,
                RequestBody = payosResult.RequestRaw,
                ResponseBody = payosResult.ResponseRaw,
                ProviderName = "PayOS",
                ProviderPayoutId = payosResult.PayoutId,
                ProviderRequestId = reference,
                ProviderTransId = payosResult.TransId,
                ProviderRequestBody = payosResult.RequestRaw,
                ProviderResponseBody = payosResult.ResponseRaw,
                CreatedAt = Common.Utils.VietnamTime.Now,
                UpdatedAt = Common.Utils.VietnamTime.Now,
                CompletedAt = payosResult.ResultCode == 0 ? Common.Utils.VietnamTime.Now : null,
                FailedAt = IsFailed(payosResult.ResultCode) ? Common.Utils.VietnamTime.Now : null
            };

            // Save payout record FIRST
            try
            {
                await _payoutRepository.AddAsync(payout);
                await _payoutRepository.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Payout save failed - rollback wallet
                await RollbackWalletAsync();
                throw new InvalidOperationException("Failed to save payout record to database. Wallet has been restored.", ex);
            }

            try
            {
                await _momoTransactionService.CreateAsync(new MomoTransaction
                {
                    RequestId = payosResult?.PayoutId ?? string.Empty,
                    PartnerCode = string.Empty,
                    Amount = request.Amount,
                    Type = "disbursement",
                    RequestBody = payosResult?.RequestRaw ?? string.Empty,
                    ResponseBody = payosResult?.ResponseRaw ?? string.Empty,
                    Status = payout.Status,
                    ResultCode = payosResult?.ResultCode,
                    Message = payosResult?.Message!,
                    CreatedAt = Common.Utils.VietnamTime.Now,
                    UpdatedAt = Common.Utils.VietnamTime.Now
                });
            }
            catch (Exception ex)
            {
                // Log transaction save failure but do not rollback payout since main payout record is saved and provider has been called
                ILogger?.LogError(ex, "Failed to save momo transaction record for payout {PayoutId}", payout.PayoutId);
            }


            var resultCode = payosResult?.ResultCode ?? -1;
            if (resultCode == 0)
            {
                await _walletService.FinalizePayoutSuccessAsync(landlordId, request.Amount);
            }
            else if (IsFailed(resultCode))
            {
                await RollbackWalletAsync();
            }

            return Map(payout);
        }
        catch
        {
            await RollbackWalletAsync();
            throw;
        }
    }

    public async Task<(IEnumerable<LandlordPayoutResponseDto> Items, int TotalCount)> GetPayoutHistoryAsync(
        Guid landlordId,
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null)
    {
        filters ??= new Dictionary<string, string>();
        filters["LandlordId"] = landlordId.ToString();

        var (items, totalCount) = await _payoutRepository.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters, ["LandlordId", "Status", "Channel"]);
        return (items.Select(Map).ToList(), totalCount);
    }

    public async Task<LandlordPayoutResponseDto?> GetPayoutByIdAsync(Guid landlordId, Guid payoutId)
    {
        var payout = await _payoutRepository.GetByIdAsync(payoutId);
        if (payout == null || payout.LandlordId != landlordId)
        {
            return null;
        }

        return Map(payout);
    }

    public async Task<int> SyncProcessingPayoutsAsync(CancellationToken cancellationToken = default)
    {
        var processing = await _payoutRepository.FindAsync(p => p.Status == "processing" || p.Status == "requested");
        var updatedCount = 0;

        foreach (var payout in processing)
        {
            PayOSPayoutResult payosQuery;

            if (string.Equals(payout.Channel, "wallet", StringComparison.OrdinalIgnoreCase))
            {
                // Wallet channel uses Momo disbursement status
                var momoQuery = await _momoService.QueryDisbursementStatusAsync(new Common.DTOs.MomoQueryDisbursementRequest
                {
                    OrderId = payout.MomoOrderId ?? string.Empty,
                    RequestId = payout.MomoRequestId
                }, cancellationToken);

                payosQuery = new PayOSPayoutResult(
                    ResultCode: momoQuery.ResultCode,
                    PayoutId: momoQuery.OrderId ?? payout.MomoOrderId,
                    Message: momoQuery.Message,
                    RequestRaw: string.Empty,
                    ResponseRaw: momoQuery.ResponseRaw,
                    TransId: momoQuery.TransId);
            }
            else
            {
                // Bank and other channels use PayOS
                payosQuery = await _payOSService.QueryBankPayoutStatusAsync(payout.MomoOrderId!, cancellationToken);
            }

            var bankStatus = MapStatus(payosQuery.ResultCode);

            if (bankStatus == payout.Status)
            {
                continue;
            }

            payout.Status = bankStatus;
            payout.ResultCode = payosQuery.ResultCode;
            payout.Message = payosQuery.Message;
            payout.MomoTransId = string.IsNullOrWhiteSpace(payosQuery.TransId) ? payout.MomoTransId : payosQuery.TransId;
            payout.ResponseBody = payosQuery.ResponseRaw;
            payout.ProviderName = string.Equals(payout.Channel, "wallet", StringComparison.OrdinalIgnoreCase) ? "Momo" : "PayOS";
            payout.ProviderPayoutId = payosQuery.PayoutId;
            payout.ProviderTransId = string.IsNullOrWhiteSpace(payosQuery.TransId) ? payout.ProviderTransId : payosQuery.TransId;
            payout.ProviderResponseBody = payosQuery.ResponseRaw;
            payout.UpdatedAt = Common.Utils.VietnamTime.Now;

            if (payosQuery.ResultCode == 0)
            {
                payout.CompletedAt = Common.Utils.VietnamTime.Now;
                await _walletService.FinalizePayoutSuccessAsync(payout.LandlordId, payout.Amount);
            }
            else if (IsFailed(payosQuery.ResultCode))
            {
                payout.FailedAt = Common.Utils.VietnamTime.Now;
                await _walletService.RollbackPayoutAsync(payout.LandlordId, payout.Amount);
            }

            _payoutRepository.Update(payout);
            updatedCount++;
        }

        if (updatedCount > 0)
        {
            await _payoutRepository.SaveChangesAsync();
        }

        return updatedCount;
    }

    private static string MapStatus(int resultCode)
    {
        if (resultCode == 0)
        {
            return "success";
        }

        if (PendingCodes.Contains(resultCode))
        {
            return "processing";
        }

        return "failed";
    }

    private static bool IsFailed(int resultCode) => resultCode != 0 && !PendingCodes.Contains(resultCode);

    private static LandlordPayoutResponseDto Map(LandlordPayout payout)
    {
        return new LandlordPayoutResponseDto
        {
            PayoutId = payout.PayoutId,
            Amount = payout.Amount,
            Status = payout.Status!,
            Message = payout.Message,
            CreatedAt = payout.CreatedAt,
            UpdatedAt = payout.UpdatedAt
        };
    }

    private async Task EnsureDailyPayoutLimitAsync(Guid landlordId)
    {
        var startOfDay = Common.Utils.VietnamTime.TodayDateTime;
        var endOfDay = startOfDay.AddDays(1);

        var todayPayouts = await _payoutRepository.FindAsync(p =>
            p.LandlordId == landlordId &&
            p.CreatedAt >= startOfDay &&
            p.CreatedAt < endOfDay &&
            p.Status != "failed"); // Only count non-failed attempts

        if (todayPayouts.Count() >= MaxDailyPayoutRequests)
        {
            throw new InvalidOperationException("You can only create up to 5 payout requests per day.");
        }
    }
}
