using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public class LandlordPayoutService : ILandlordPayoutService
{
    private static readonly HashSet<int> PendingCodes = [7000, 7002];

    private readonly IRepository<LandlordPayout> _payoutRepository;
    private readonly IRepository<Landlord> _landlordRepository;
    private readonly IMomoService _momoService;
    private readonly ILandlordWalletService _walletService;
    private readonly IMomoTransactionService _momoTransactionService;

    public LandlordPayoutService(
        IRepository<LandlordPayout> payoutRepository,
        IRepository<Landlord> landlordRepository,
        IMomoService momoService,
        ILandlordWalletService walletService,
        IMomoTransactionService momoTransactionService)
    {
        _payoutRepository = payoutRepository;
        _landlordRepository = landlordRepository;
        _momoService = momoService;
        _walletService = walletService;
        _momoTransactionService = momoTransactionService;
    }

    public async Task<LandlordPayoutResponseDto> CreatePayoutAsync(Guid landlordId, CreateLandlordPayoutRequestDto request, CancellationToken cancellationToken = default)
    {
        var landlord = await _landlordRepository.GetByIdAsync(landlordId)
            ?? throw new ArgumentException("Landlord profile not found.");

        var channel = NormalizeChannel(request.Channel);
        ValidateAmountByChannel(channel, request.Amount);
        ValidateProfileForChannel(landlord, channel);

        await _walletService.ReserveForPayoutAsync(landlordId, request.Amount);

        var momoRequest = BuildMomoDisbursementRequest(landlord, request, channel);
        var momoResponse = await _momoService.CreateDisbursementAsync(momoRequest, cancellationToken);

        var payout = new LandlordPayout
        {
            PayoutId = Guid.NewGuid(),
            LandlordId = landlordId,
            Amount = request.Amount,
            FeeAmount = 0,
            NetAmount = request.Amount,
            Channel = channel,
            Status = MapStatus(momoResponse.ResultCode),
            MomoOrderId = momoResponse.OrderId,
            MomoRequestId = momoResponse.RequestId,
            MomoTransId = momoResponse.TransId,
            ResultCode = momoResponse.ResultCode,
            Message = momoResponse.Message,
            RequestBody = momoResponse.RequestRaw,
            ResponseBody = momoResponse.ResponseRaw,
            CreatedAt = Common.Utils.VietnamTime.Now,
            UpdatedAt = Common.Utils.VietnamTime.Now,
            CompletedAt = momoResponse.ResultCode == 0 ? Common.Utils.VietnamTime.Now : null,
            FailedAt = IsFailed(momoResponse.ResultCode) ? Common.Utils.VietnamTime.Now : null
        };

        await _payoutRepository.AddAsync(payout);
        await _payoutRepository.SaveChangesAsync();

        await _momoTransactionService.CreateAsync(new MomoTransaction
        {
            RequestId = momoResponse.RequestId,
            PartnerCode = string.Empty,
            Amount = request.Amount,
            Type = "disbursement",
            RequestBody = momoResponse.RequestRaw ?? string.Empty,
            ResponseBody = momoResponse.ResponseRaw ?? string.Empty,
            Status = payout.Status,
            ResultCode = momoResponse.ResultCode,
            Message = momoResponse.Message,
            CreatedAt = Common.Utils.VietnamTime.Now,
            UpdatedAt = Common.Utils.VietnamTime.Now
        });

        if (momoResponse.ResultCode == 0)
        {
            await _walletService.FinalizePayoutSuccessAsync(landlordId, request.Amount);
        }
        else if (IsFailed(momoResponse.ResultCode))
        {
            await _walletService.RollbackPayoutAsync(landlordId, request.Amount);
        }

        return Map(payout);
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
            var queryResult = await _momoService.QueryDisbursementStatusAsync(new MomoQueryDisbursementRequest
            {
                OrderId = payout.MomoOrderId,
                RequestId = payout.MomoRequestId,
                Lang = "vi"
            }, cancellationToken);

            var newStatus = MapStatus(queryResult.ResultCode);
            if (newStatus == payout.Status)
            {
                continue;
            }

            payout.Status = newStatus;
            payout.ResultCode = queryResult.ResultCode;
            payout.Message = queryResult.Message;
            payout.MomoTransId = string.IsNullOrWhiteSpace(queryResult.TransId) ? payout.MomoTransId : queryResult.TransId;
            payout.ResponseBody = queryResult.ResponseRaw;
            payout.UpdatedAt = Common.Utils.VietnamTime.Now;

            if (queryResult.ResultCode == 0)
            {
                payout.CompletedAt = Common.Utils.VietnamTime.Now;
                await _walletService.FinalizePayoutSuccessAsync(payout.LandlordId, payout.Amount);
            }
            else if (IsFailed(queryResult.ResultCode))
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

    private static MomoDisbursementRequest BuildMomoDisbursementRequest(Landlord landlord, CreateLandlordPayoutRequestDto request, string channel)
    {
        if (channel == "bank")
        {
            var receiver = !string.IsNullOrWhiteSpace(landlord.PayoutBankAccountNo)
                ? landlord.PayoutBankAccountNo!
                : landlord.PayoutBankCardNo!;

            return new MomoDisbursementRequest
            {
                Amount = request.Amount,
                OrderInfo = string.IsNullOrWhiteSpace(request.OrderInfo) ? "Landlord payout" : request.OrderInfo,
                RequestType = "disburseToBank",
                ReceiverAccount = receiver,
                ReceiverName = landlord.PayoutReceiverName!,
                BankCode = landlord.PayoutBankCode,
                ExtraData = string.Empty,
                Lang = "vi"
            };
        }

        return new MomoDisbursementRequest
        {
            Amount = request.Amount,
            OrderInfo = string.IsNullOrWhiteSpace(request.OrderInfo) ? "Landlord payout" : request.OrderInfo,
            RequestType = "disburseToWallet",
            ReceiverAccount = landlord.MomoWalletPhone!,
            ReceiverName = landlord.PayoutReceiverName!,
            PersonalId = landlord.PayoutPersonalId,
            ExtraData = string.Empty,
            Lang = "vi"
        };
    }

    private static void ValidateProfileForChannel(Landlord landlord, string channel)
    {
        if (string.IsNullOrWhiteSpace(landlord.PayoutReceiverName))
        {
            throw new InvalidOperationException("Payout receiver name is required. Please update your payout profile.");
        }

        if (channel == "bank")
        {
            var hasBankTarget = !string.IsNullOrWhiteSpace(landlord.PayoutBankAccountNo) || !string.IsNullOrWhiteSpace(landlord.PayoutBankCardNo);
            if (!hasBankTarget || string.IsNullOrWhiteSpace(landlord.PayoutBankCode))
            {
                throw new InvalidOperationException("Bank payout profile is incomplete. Please provide bank account/card and bank code.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(landlord.MomoWalletPhone))
        {
            throw new InvalidOperationException("MoMo wallet phone is required for wallet payout.");
        }
    }

    private static void ValidateAmountByChannel(string channel, long amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero.");
        }

        if (channel == "wallet")
        {
            if (amount < 1000 || amount > 200000000)
            {
                throw new ArgumentException("Wallet payout amount must be between 1,000 and 200,000,000 VND.");
            }
            return;
        }

        if (amount < 10000 || amount > 20000000)
        {
            throw new ArgumentException("Bank payout amount must be between 10,000 and 20,000,000 VND.");
        }
    }

    private static string NormalizeChannel(string? channel)
    {
        return (channel ?? "wallet").Trim().ToLowerInvariant() switch
        {
            "bank" => "bank",
            _ => "wallet"
        };
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
            FeeAmount = payout.FeeAmount,
            NetAmount = payout.NetAmount,
            Channel = payout.Channel,
            Status = payout.Status,
            MomoOrderId = payout.MomoOrderId,
            MomoRequestId = payout.MomoRequestId,
            MomoTransId = payout.MomoTransId,
            ResultCode = payout.ResultCode,
            Message = payout.Message,
            CreatedAt = payout.CreatedAt,
            UpdatedAt = payout.UpdatedAt
        };
    }
}
