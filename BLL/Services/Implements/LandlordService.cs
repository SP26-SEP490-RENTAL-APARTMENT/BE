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
        return await _apartmentRepository.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters, null);
    }

    public async Task<SubscriptionPlanDto?> GetCurrentSubscriptionAsync(Guid landlordId)
    {
        var landlord = await _landlordRepository.GetByIdAsync(landlordId);
        if (landlord == null || !landlord.CurrentPlanId.HasValue)
            return null;

        var plan = await _subscriptionPlanService.GetByIdAsync(landlord.CurrentPlanId.Value);
        return plan == null ? null : _mapper.Map<SubscriptionPlanDto>(plan);
    }
}
