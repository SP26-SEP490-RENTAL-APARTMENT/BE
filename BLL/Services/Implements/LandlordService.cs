using System.Linq;
using AutoMapper;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.AspNetCore.Http;

namespace BLL.Services.Implements;

public class LandlordService : BaseService<Landlord>, ILandlordService
{
    private readonly IRepository<Landlord> _landlordRepository;
    private readonly IApartmentRepository _apartmentRepository;
    private readonly IImageService _imageService;
    private readonly IApartmentMediumService _apartmentMediumService;
    private readonly ISubscriptionPlanService _subscriptionPlanService;
    private readonly IMapper _mapper;

    public LandlordService(
        IRepository<Landlord> repository,
        IApartmentRepository apartmentRepository,
        IImageService imageService,
        IApartmentMediumService apartmentMediumService,
        ISubscriptionPlanService subscriptionPlanService,
        IMapper mapper) : base(repository)
    {
        _landlordRepository = repository;
        _apartmentRepository = apartmentRepository;
        _imageService = imageService;
        _apartmentMediumService = apartmentMediumService;
        _subscriptionPlanService = subscriptionPlanService;
        _mapper = mapper;
    }

    public async Task<Landlord?> GetByUserIdAsync(Guid userId)
    {
        var results = await _landlordRepository.FindAsync(l => l.LandlordNavigation.UserId == userId);
        return results.FirstOrDefault();
    }

    public async Task<(IEnumerable<Apartment> Items, int TotalCount)> GetOwnApartmentsAsync(
        Guid landlordId,
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null)
    {
        var filters = new Dictionary<string, string> { { "LandlordId", landlordId.ToString() } };
        var allowedColumns = new[]
        {
            "ApartmentId",
            "LandlordId",
            "Title",
            "Description",
            "Address",
            "District",
            "City",
            "Status",
            "BasePricePerNight",
            "CreatedAt"
        };

        return await _apartmentRepository.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters, allowedColumns);
    }

    public async Task<SubscriptionPlanDto?> GetCurrentSubscriptionAsync(Guid landlordId)
    {
        var landlord = await _landlordRepository.GetByIdAsync(landlordId);
        if (landlord == null || !landlord.CurrentPlanId.HasValue)
            return null;

        var plan = await _subscriptionPlanService.GetByIdAsync(landlord.CurrentPlanId.Value);
        return plan == null ? null : _mapper.Map<SubscriptionPlanDto>(plan);
    }

    public async Task<LandlordPayoutProfileDto?> GetPayoutProfileAsync(Guid landlordId)
    {
        var landlord = await _landlordRepository.GetByIdAsync(landlordId);
        if (landlord == null)
        {
            return null;
        }

        return new LandlordPayoutProfileDto
        {
            MomoWalletPhone = landlord.MomoWalletPhone,
            ReceiverName = landlord.PayoutReceiverName,
            PersonalIdMasked = MaskRight(landlord.PayoutPersonalId, 4),
            BankAccountNoMasked = MaskRight(landlord.PayoutBankAccountNo, 4),
            BankCardNoMasked = MaskRight(landlord.PayoutBankCardNo, 4),
            BankCode = landlord.PayoutBankCode,
            PreferredPayoutMethod = landlord.PreferredPayoutMethod
        };
    }

    public async Task<LandlordPayoutProfileDto> UpsertPayoutProfileAsync(Guid landlordId, UpsertLandlordPayoutProfileRequestDto request)
    {
        var landlord = await _landlordRepository.GetByIdAsync(landlordId)
            ?? throw new ArgumentException("Landlord profile not found.");

        landlord.MomoWalletPhone = request.MomoWalletPhone?.Trim();
        landlord.PayoutReceiverName = request.ReceiverName?.Trim();
        landlord.PayoutPersonalId = request.PersonalId?.Trim();
        landlord.PayoutBankAccountNo = request.BankAccountNo?.Trim();
        landlord.PayoutBankCardNo = request.BankCardNo?.Trim();
        landlord.PayoutBankCode = request.BankCode?.Trim();
        landlord.PreferredPayoutMethod = request.PreferredPayoutMethod?.Trim();

        _landlordRepository.Update(landlord);
        await _landlordRepository.SaveChangesAsync();

        return (await GetPayoutProfileAsync(landlordId))!;
    }

    public async Task<LandlordPayoutProfileDto> UpdateBankPayoutProfileAsync(Guid landlordId, UpdateBankPayoutProfileRequestDto request)
    {
        var landlord = await _landlordRepository.GetByIdAsync(landlordId)
            ?? throw new ArgumentException("Landlord profile not found.");

        ValidateBankPayoutProfileRequest(request);

        landlord.PayoutReceiverName = request.ReceiverName?.Trim();
        landlord.PayoutPersonalId = request.PersonalId?.Trim();
        landlord.PayoutBankAccountNo = request.BankAccountNo?.Trim();
        landlord.PayoutBankCardNo = request.BankCardNo?.Trim();
        landlord.PayoutBankCode = request.BankCode?.Trim();
        landlord.PreferredPayoutMethod = request.PreferredPayoutMethod?.Trim();

        _landlordRepository.Update(landlord);
        await _landlordRepository.SaveChangesAsync();

        return (await GetPayoutProfileAsync(landlordId))!;
    }

    public async Task<LandlordPayoutProfileDto> UpdateMomoPayoutProfileAsync(Guid landlordId, UpdateMomoPayoutProfileRequestDto request)
    {
        var landlord = await _landlordRepository.GetByIdAsync(landlordId)
            ?? throw new ArgumentException("Landlord profile not found.");

        ValidateMomoPayoutProfileRequest(request);

        landlord.MomoWalletPhone = request.MomoWalletPhone?.Trim();
        landlord.PayoutReceiverName = request.ReceiverName?.Trim();
        landlord.PayoutPersonalId = request.PersonalId?.Trim();
        landlord.PreferredPayoutMethod = request.PreferredPayoutMethod?.Trim();

        _landlordRepository.Update(landlord);
        await _landlordRepository.SaveChangesAsync();

        return (await GetPayoutProfileAsync(landlordId))!;
    }

    private static void ValidateBankPayoutProfileRequest(UpdateBankPayoutProfileRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.ReceiverName))
        {
            throw new ArgumentException("ReceiverName is required for bank payout profile.");
        }

        if (string.IsNullOrWhiteSpace(request.BankCode))
        {
            throw new ArgumentException("BankCode is required for bank payout profile.");
        }

        var hasAccountNo = !string.IsNullOrWhiteSpace(request.BankAccountNo);
        var hasCardNo = !string.IsNullOrWhiteSpace(request.BankCardNo);
        if (!hasAccountNo && !hasCardNo)
        {
            throw new ArgumentException("Either BankAccountNo or BankCardNo is required for bank payout profile.");
        }
    }

    private static void ValidateMomoPayoutProfileRequest(UpdateMomoPayoutProfileRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.ReceiverName))
        {
            throw new ArgumentException("ReceiverName is required for MoMo payout profile.");
        }

        if (string.IsNullOrWhiteSpace(request.MomoWalletPhone))
        {
            throw new ArgumentException("MomoWalletPhone is required for MoMo payout profile.");
        }
    }

    private static string? MaskRight(string? value, int visibleTail)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (value.Length <= visibleTail)
        {
            return new string('*', value.Length);
        }

        return new string('*', value.Length - visibleTail) + value[^visibleTail..];
    }
}
