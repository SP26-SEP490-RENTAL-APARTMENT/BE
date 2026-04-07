using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using BLL.Services.Implements;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Tests;

public class LandlordPayoutServiceTests
{
    [Fact]
    public async Task CreatePayoutAsync_Success_FinalizesWalletAndCreatesTransaction()
    {
        var landlordId = Guid.NewGuid();
        var landlordRepo = new InMemoryRepository<Landlord>(l => l.LandlordId, new Landlord
        {
            LandlordId = landlordId,
            PayoutReceiverName = "Landlord A",
            MomoWalletPhone = "0900000001"
        });

        var payoutRepo = new InMemoryRepository<LandlordPayout>(p => p.PayoutId);
        var momoService = new FakeMomoService
        {
            CreateDisbursementResult = new MomoDisbursementResponse
            {
                ResultCode = 0,
                Message = "Success",
                OrderId = "ORD-1",
                RequestId = "REQ-1",
                TransId = "TRX-1",
                Amount = 1500,
                RequestRaw = "{}",
                ResponseRaw = "{}"
            }
        };

        var walletService = new RecordingWalletService();
        var momoTransactionService = new InMemoryMomoTransactionService();

        var sut = new LandlordPayoutService(payoutRepo, landlordRepo, momoService, walletService, momoTransactionService);
        var request = new CreateLandlordPayoutRequestDto { Amount = 1500, Channel = "wallet", OrderInfo = "payout" };

        var result = await sut.CreatePayoutAsync(landlordId, request, CancellationToken.None);

        Assert.Equal("success", result.Status);
        Assert.Equal(1500, result.Amount);
        Assert.Single(walletService.ReservedAmounts);
        Assert.Single(walletService.FinalizedAmounts);
        Assert.Empty(walletService.RolledBackAmounts);
        Assert.Single(payoutRepo.Items);
        Assert.Single(momoTransactionService.Items);
        Assert.Equal("success", momoTransactionService.Items[0].Status);
    }

    [Fact]
    public async Task CreatePayoutAsync_Failed_RollsBackWallet()
    {
        var landlordId = Guid.NewGuid();
        var landlordRepo = new InMemoryRepository<Landlord>(l => l.LandlordId, new Landlord
        {
            LandlordId = landlordId,
            PayoutReceiverName = "Landlord B",
            MomoWalletPhone = "0900000002"
        });

        var payoutRepo = new InMemoryRepository<LandlordPayout>(p => p.PayoutId);
        var momoService = new FakeMomoService
        {
            CreateDisbursementResult = new MomoDisbursementResponse
            {
                ResultCode = 42,
                Message = "Failure",
                OrderId = "ORD-2",
                RequestId = "REQ-2",
                Amount = 1500,
                RequestRaw = "{}",
                ResponseRaw = "{}"
            }
        };

        var walletService = new RecordingWalletService();
        var momoTransactionService = new InMemoryMomoTransactionService();

        var sut = new LandlordPayoutService(payoutRepo, landlordRepo, momoService, walletService, momoTransactionService);
        var request = new CreateLandlordPayoutRequestDto { Amount = 1500, Channel = "wallet", OrderInfo = "payout" };

        var result = await sut.CreatePayoutAsync(landlordId, request, CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Single(walletService.ReservedAmounts);
        Assert.Empty(walletService.FinalizedAmounts);
        Assert.Single(walletService.RolledBackAmounts);
        Assert.Single(payoutRepo.Items);
        Assert.Single(momoTransactionService.Items);
        Assert.Equal("failed", momoTransactionService.Items[0].Status);
    }

    [Fact]
    public async Task SyncProcessingPayoutsAsync_TransitionsToSuccess_FinalizesWallet()
    {
        var landlordId = Guid.NewGuid();
        var payout = new LandlordPayout
        {
            PayoutId = Guid.NewGuid(),
            LandlordId = landlordId,
            Amount = 2000,
            FeeAmount = 0,
            NetAmount = 2000,
            Channel = "wallet",
            Status = "processing",
            MomoOrderId = "ORD-3",
            MomoRequestId = "REQ-3",
            MomoTransId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var payoutRepo = new InMemoryRepository<LandlordPayout>(p => p.PayoutId, payout);
        var landlordRepo = new InMemoryRepository<Landlord>(l => l.LandlordId);
        var momoService = new FakeMomoService
        {
            QueryDisbursementResult = new MomoQueryDisbursementResponse
            {
                ResultCode = 0,
                Message = "Completed",
                OrderId = "ORD-3",
                RequestId = "REQ-3",
                TransId = "TRX-3",
                ResponseRaw = "{}"
            }
        };

        var walletService = new RecordingWalletService();
        var momoTransactionService = new InMemoryMomoTransactionService();

        var sut = new LandlordPayoutService(payoutRepo, landlordRepo, momoService, walletService, momoTransactionService);

        var updated = await sut.SyncProcessingPayoutsAsync(CancellationToken.None);

        Assert.Equal(1, updated);
        Assert.Equal("success", payout.Status);
        Assert.NotNull(payout.CompletedAt);
        Assert.Single(walletService.FinalizedAmounts);
        Assert.Empty(walletService.RolledBackAmounts);
    }

    [Fact]
    public async Task SyncProcessingPayoutsAsync_TransitionsToFailed_RollsBackWallet()
    {
        var landlordId = Guid.NewGuid();
        var payout = new LandlordPayout
        {
            PayoutId = Guid.NewGuid(),
            LandlordId = landlordId,
            Amount = 3000,
            FeeAmount = 0,
            NetAmount = 3000,
            Channel = "wallet",
            Status = "requested",
            MomoOrderId = "ORD-4",
            MomoRequestId = "REQ-4",
            MomoTransId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var payoutRepo = new InMemoryRepository<LandlordPayout>(p => p.PayoutId, payout);
        var landlordRepo = new InMemoryRepository<Landlord>(l => l.LandlordId);
        var momoService = new FakeMomoService
        {
            QueryDisbursementResult = new MomoQueryDisbursementResponse
            {
                ResultCode = 99,
                Message = "Rejected",
                OrderId = "ORD-4",
                RequestId = "REQ-4",
                TransId = null,
                ResponseRaw = "{}"
            }
        };

        var walletService = new RecordingWalletService();
        var momoTransactionService = new InMemoryMomoTransactionService();

        var sut = new LandlordPayoutService(payoutRepo, landlordRepo, momoService, walletService, momoTransactionService);

        var updated = await sut.SyncProcessingPayoutsAsync(CancellationToken.None);

        Assert.Equal(1, updated);
        Assert.Equal("failed", payout.Status);
        Assert.NotNull(payout.FailedAt);
        Assert.Empty(walletService.FinalizedAmounts);
        Assert.Single(walletService.RolledBackAmounts);
    }
}

public class LandlordWalletServiceTests
{
    [Fact]
    public async Task ApplyOccupiedIncidentPenaltyAsync_UsesAvailableThenPending_WhenFundsAreEnough()
    {
        var landlordId = Guid.NewGuid();
        var walletRepo = new InMemoryRepository<LandlordWallet>(
            w => w.LandlordId,
            new LandlordWallet
            {
                LandlordId = landlordId,
                AvailableBalance = 100m,
                PendingBalance = 50m,
                UpdatedAt = DateTime.UtcNow
            }
        );

        var sut = new LandlordWalletService(walletRepo);

        var result = await sut.ApplyOccupiedIncidentPenaltyAsync(landlordId, 120m);

        var wallet = walletRepo.Items.Single();
        Assert.Equal(0m, wallet.AvailableBalance);
        Assert.Equal(30m, wallet.PendingBalance);
        Assert.Equal(100m, result.DeductedFromAvailable);
        Assert.Equal(20m, result.DeductedFromPending);
        Assert.Equal(0m, result.DebtRecorded);
    }

    [Fact]
    public async Task ApplyOccupiedIncidentPenaltyAsync_RecordsDebt_WhenFundsAreInsufficient()
    {
        var landlordId = Guid.NewGuid();
        var walletRepo = new InMemoryRepository<LandlordWallet>(
            w => w.LandlordId,
            new LandlordWallet
            {
                LandlordId = landlordId,
                AvailableBalance = 10m,
                PendingBalance = 5m,
                UpdatedAt = DateTime.UtcNow
            }
        );

        var sut = new LandlordWalletService(walletRepo);

        var result = await sut.ApplyOccupiedIncidentPenaltyAsync(landlordId, 40m);

        var wallet = walletRepo.Items.Single();
        Assert.Equal(-25m, wallet.AvailableBalance);
        Assert.Equal(0m, wallet.PendingBalance);
        Assert.Equal(10m, result.DeductedFromAvailable);
        Assert.Equal(5m, result.DeductedFromPending);
        Assert.Equal(25m, result.DebtRecorded);
    }
}

internal sealed class InMemoryRepository<T> : IRepository<T>
    where T : class
{
    private readonly Func<T, Guid>? _idSelector;

    public InMemoryRepository(Func<T, Guid>? idSelector = null, params T[] seed)
    {
        _idSelector = idSelector;
        Items = seed.ToList();
    }

    public List<T> Items { get; }

    public Task AddAsync(T entity)
    {
        Items.Add(entity);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        var compiled = predicate.Compile();
        IEnumerable<T> result = Items.Where(compiled);
        return Task.FromResult(result);
    }

    public Task<(IEnumerable<T> Items, int TotalCount)> GetAllAsync(
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null,
        IEnumerable<string>? allowedColumns = null)
    {
        return Task.FromResult((Items.AsEnumerable(), Items.Count));
    }

    public Task<T?> GetByIdAsync(Guid id)
    {
        if (_idSelector == null)
        {
            return Task.FromResult<T?>(null);
        }

        var entity = Items.FirstOrDefault(x => _idSelector(x) == id);
        return Task.FromResult(entity);
    }

    public void Remove(T entity)
    {
        Items.Remove(entity);
    }

    public void Update(T entity)
    {
        // No-op for in-memory reference updates.
    }

    public Task<int> SaveChangesAsync()
    {
        return Task.FromResult(1);
    }
}

internal sealed class FakeMomoService : IMomoService
{
    public MomoDisbursementResponse CreateDisbursementResult { get; set; } = new();

    public MomoQueryDisbursementResponse QueryDisbursementResult { get; set; } = new();

    public Task<MomoCreatePaymentResponse> CreateWalletPaymentAsync(MomoCreatePaymentRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<MomoDisbursementResponse> VerifyWalletAsync(MomoVerifyWalletRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<MomoDisbursementResponse> CreateDisbursementAsync(MomoDisbursementRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateDisbursementResult);
    }

    public Task<MomoQueryDisbursementResponse> QueryDisbursementStatusAsync(MomoQueryDisbursementRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(QueryDisbursementResult);
    }

    public bool ValidateDisbursementIpnSignature(string requestBody)
    {
        throw new NotImplementedException();
    }
}

internal sealed class RecordingWalletService : ILandlordWalletService
{
    public List<long> ReservedAmounts { get; } = new();

    public List<long> FinalizedAmounts { get; } = new();

    public List<long> RolledBackAmounts { get; } = new();

    public Task<LandlordWallet> GetOrCreateAsync(Guid landlordId)
    {
        return Task.FromResult(new LandlordWallet { LandlordId = landlordId });
    }

    public Task CreditPendingAsync(Guid landlordId, decimal amount)
    {
        return Task.CompletedTask;
    }

    public Task DebitAvailableAsync(Guid landlordId, decimal amount)
    {
        return Task.CompletedTask;
    }

    public Task<LandlordPenaltyApplicationResultDto> ApplyOccupiedIncidentPenaltyAsync(Guid landlordId, decimal amount)
    {
        throw new NotImplementedException();
    }

    public Task ReserveForPayoutAsync(Guid landlordId, long amount)
    {
        ReservedAmounts.Add(amount);
        return Task.CompletedTask;
    }

    public Task FinalizePayoutSuccessAsync(Guid landlordId, long amount)
    {
        FinalizedAmounts.Add(amount);
        return Task.CompletedTask;
    }

    public Task RollbackPayoutAsync(Guid landlordId, long amount)
    {
        RolledBackAmounts.Add(amount);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryMomoTransactionService : IMomoTransactionService
{
    public List<MomoTransaction> Items { get; } = new();

    public Task<MomoTransaction?> GetByIdAsync(Guid id)
    {
        return Task.FromResult<MomoTransaction?>(null);
    }

    public Task<(IEnumerable<MomoTransaction> Items, int TotalCount)> GetAllAsync(
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null,
        IEnumerable<string>? allowedColumns = null)
    {
        return Task.FromResult((Items.AsEnumerable(), Items.Count));
    }

    public Task<MomoTransaction> CreateAsync(MomoTransaction entity)
    {
        Items.Add(entity);
        return Task.FromResult(entity);
    }

    public Task UpdateAsync(MomoTransaction entity)
    {
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        return Task.CompletedTask;
    }

    public Task<MomoTransaction?> FindByRequestIdAsync(string requestId)
    {
        var result = Items.FirstOrDefault(i => i.RequestId == requestId);
        return Task.FromResult(result);
    }

    public Task<MomoTransaction?> FindByRequestBodyContainsAsync(string content)
    {
        var result = Items.FirstOrDefault(i => i.RequestBody.Contains(content, StringComparison.Ordinal));
        return Task.FromResult(result);
    }
}
