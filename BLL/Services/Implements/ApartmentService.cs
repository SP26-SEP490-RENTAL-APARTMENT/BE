using AutoMapper;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;
using NotificationType = Common.Enums.Notification;
using ApartmentBookingStatusEnum = Common.Enums.ApartmentBookingStatus;

namespace BLL.Services.Implements;

public class ApartmentService : BaseService<Apartment>, IApartmentService
{
    private readonly IImageService _imageService;
    private readonly IApartmentMediumService _apartmentMediumService;
    private readonly IMapper _mapper;
    private readonly IApartmentPriceCalendarRepository _apartmentPriceCalendarRepository;
    private readonly IHolidayService _holidayService;

    private readonly IApartmentRepository _apartmentRepository;
    private readonly ITenantWishlistRepository _tenantWishlistRepository;
    private readonly IAmenityRepository _amenityRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRepository<Notification> _notificationRepository;
    private readonly IRepository<PropertyInspection> _propertyInspectionRepository;

    public ApartmentService(
        IApartmentRepository repository,
        ITenantWishlistRepository tenantWishlistRepository,
        IAmenityRepository amenityRepository,
        IImageService imageService,
        IApartmentMediumService apartmentMediumService,
        IMapper mapper,
        IUserRepository userRepository,
        IRepository<Notification> notificationRepository,
        IRepository<PropertyInspection> propertyInspectionRepository,
        IApartmentPriceCalendarRepository apartmentPriceCalendarRepository,
        IHolidayService holidayService)
        : base(repository)
    {
        _apartmentRepository = repository;
        _tenantWishlistRepository = tenantWishlistRepository;
        _amenityRepository = amenityRepository;
        _imageService = imageService;
        _apartmentMediumService = apartmentMediumService;
        _mapper = mapper;
        _userRepository = userRepository;
        _notificationRepository = notificationRepository;
        _propertyInspectionRepository = propertyInspectionRepository;
        _apartmentPriceCalendarRepository = apartmentPriceCalendarRepository;
        _holidayService = holidayService;
    }

    public override async Task<(IEnumerable<Apartment> Items, int TotalCount)> GetAllAsync(
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null,
        IEnumerable<string>? allowedColumns = null)
    {
        var effectiveAllowedColumns = new[]
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
            "CreatedAt",
            "MaxOccupants",
            "IsPetAllowed"
        };

        return await base.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters, effectiveAllowedColumns);
    }

    public async Task<(IEnumerable<Apartment> Items, int TotalCount)> GetAllPublicAsync(
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null,
        DateOnly? checkInDate = null,
        DateOnly? checkOutDate = null)
    {
        var effectiveAllowedColumns = new[]
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
            "CreatedAt",
            "MaxOccupants",
            "IsPetAllowed"
        };

        return await _apartmentRepository.GetAllPublicAsync(page, pageSize, sortBy, sortOrder, search, filters, effectiveAllowedColumns, checkInDate, checkOutDate);
    }

    public async Task<(IEnumerable<ApartmentResponseDto> Items, int TotalCount)> GetAllPublicResponseAsync(
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null,
        Guid? tenantId = null,
        DateOnly? checkInDate = null,
        DateOnly? checkOutDate = null)
    {
        var (items, totalCount) = await GetAllPublicAsync(page, pageSize, sortBy, sortOrder, search, filters, checkInDate, checkOutDate);
        var mappedItems = _mapper.Map<List<ApartmentResponseDto>>(items);

        await ApplyPriceChangeHistoryAsync(mappedItems);
        await ApplyWishlistMetadataAsync(mappedItems, tenantId);
        return (mappedItems, totalCount);
    }

    private async Task ApplyPriceChangeHistoryAsync(List<ApartmentResponseDto> apartments)
    {
        if (apartments.Count == 0)
        {
            return;
        }

        foreach (var apartment in apartments)
        {
            var priceCalendars = await _apartmentPriceCalendarRepository.FindAsync(record => record.ApartmentId == apartment.ApartmentId);
            apartment.PriceChanges = await BuildPriceChangesAsync(apartment, priceCalendars);
        }
    }

    private async Task<List<ApartmentPriceChangeDto>> BuildPriceChangesAsync(
        ApartmentResponseDto apartment,
        IEnumerable<ApartmentPriceCalendar> priceCalendars)
    {
        var sortedCalendars = priceCalendars
            .OrderBy(record => record.StartDate)
            .ThenBy(record => record.EndDate)
            .ThenBy(record => record.CreatedAt ?? DateTime.MinValue)
            .ToList();

        var priceChanges = new List<ApartmentPriceChangeDto>();
        var previousPrice = apartment.BasePricePerNight;

        foreach (var calendar in sortedCalendars)
        {
            var newPrice = ResolveCalendarPrice(apartment.BasePricePerNight, calendar);
            var reasonText = await ResolveReasonAsync(calendar);

            // Check if we can extend the last entry (consolidation)
            if (priceChanges.Count > 0)
            {
                var lastEntry = priceChanges[priceChanges.Count - 1];
                if (lastEntry.NewPricePerNight == newPrice &&
                    lastEntry.EndDate >= calendar.StartDate.AddDays(-1))
                {
                    // Extend the last entry to include this consecutive period
                    lastEntry.EndDate = calendar.EndDate;

                    // Update reason to reflect the latest calendar row's price type
                    if (!string.Equals(lastEntry.Reason, reasonText, StringComparison.OrdinalIgnoreCase))
                    {
                        lastEntry.Reason = reasonText;
                    }

                    previousPrice = newPrice;
                    continue;
                }
            }

            // If price hasn't changed, skip
            if (newPrice == previousPrice)
            {
                continue;
            }

            // New price change - add entry
            priceChanges.Add(new ApartmentPriceChangeDto
            {
                OldPricePerNight = previousPrice,
                NewPricePerNight = newPrice,
                Reason = reasonText,
                StartDate = calendar.StartDate,
                EndDate = calendar.EndDate
            });

            previousPrice = newPrice;
        }

        return priceChanges;
    }

    private async Task<string?> ResolveReasonAsync(ApartmentPriceCalendar calendar)
    {
        var priceType = calendar.PriceType;
        if (!string.IsNullOrWhiteSpace(priceType))
        {
            if (priceType.StartsWith("pricing_policy:", StringComparison.OrdinalIgnoreCase))
            {
                var parameterKey = priceType.Substring("pricing_policy:".Length);
                return string.IsNullOrWhiteSpace(parameterKey) ? null : parameterKey;
            }

            if (priceType.Equals("pricing_policy", StringComparison.OrdinalIgnoreCase))
            {
                return await ResolvePricingPolicyFallbackReasonAsync(calendar.StartDate);
            }

            return priceType.Replace('_', ' ');
        }

        if (calendar.PricingPolicyId != Guid.Empty || calendar.VersionId != Guid.Empty)
        {
            return await ResolvePricingPolicyFallbackReasonAsync(calendar.StartDate);
        }

        return null;
    }

    private async Task<string> ResolvePricingPolicyFallbackReasonAsync(DateOnly date)
    {
        if (await _holidayService.IsHolidayAsync(date))
        {
            return "holiday_multiplier";
        }

        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return "weekend_multiplier";
        }

        return "multiplier";
    }

    private static string? ExtractReasonFromPriceType(string? priceType)
    {
        if (string.IsNullOrWhiteSpace(priceType))
            return null;

        // Handle pricing_policy with parameter key format: "pricing_policy:multiplier", "pricing_policy:weekend_multiplier", etc.
        if (priceType.StartsWith("pricing_policy:", StringComparison.OrdinalIgnoreCase))
        {
            var parameterKey = priceType.Substring("pricing_policy:".Length);
            return string.IsNullOrWhiteSpace(parameterKey) ? null : parameterKey;
        }

        // Handle old format "pricing_policy" without parameter key - default to "multiplier"
        if (priceType.Equals("pricing_policy", StringComparison.OrdinalIgnoreCase))
        {
            return "multiplier";
        }

        // For other price types, replace underscores with spaces
        return priceType.Replace('_', ' ');
    }

    private static decimal ResolveCalendarPrice(decimal basePricePerNight, ApartmentPriceCalendar calendar)
    {
        if (calendar.FixedPricePerNight.HasValue)
        {
            return calendar.FixedPricePerNight.Value;
        }

        if (calendar.DiscountPercentage.HasValue && calendar.DiscountPercentage.Value > 0)
        {
            var discountedPrice = basePricePerNight * (1 - (calendar.DiscountPercentage.Value / 100m));
            return Math.Round(discountedPrice, 2, MidpointRounding.AwayFromZero);
        }

        return basePricePerNight;
    }

    public async Task AddAmenitiesAsync(Guid apartmentId, List<Guid> amenityIds)
    {
        if (amenityIds == null || amenityIds.Count == 0)
        {
            throw new ArgumentException("At least one amenity is required.");
        }

        var uniqueAmenityIds = amenityIds.Distinct().ToList();

        var apartment = await _apartmentRepository.GetApartmentWithDetailsAsync(apartmentId);
        if (apartment == null)
        {
            throw new ArgumentException("Apartment not found.");
        }

        foreach (var amenityId in uniqueAmenityIds)
        {
            if (!apartment.Amenities.Any(a => a.AmenityId == amenityId))
            {
                var amenity = await _amenityRepository.GetByIdAsync(amenityId);
                if (amenity != null)
                {
                    apartment.Amenities.Add(amenity);
                }
            }
        }

        _apartmentRepository.Update(apartment);
        await _apartmentRepository.SaveChangesAsync();
    }

    public async Task RemoveAmenitiesAsync(Guid apartmentId, List<Guid> amenityIds)
    {
        if (amenityIds == null || amenityIds.Count == 0)
        {
            throw new ArgumentException("At least one amenity is required.");
        }

        var uniqueAmenityIds = amenityIds.Distinct().ToHashSet();

        var apartment = await _apartmentRepository.GetApartmentWithDetailsAsync(apartmentId);
        if (apartment == null)
        {
            throw new ArgumentException("Apartment not found.");
        }

        var amenitiesToRemove = apartment.Amenities
            .Where(a => uniqueAmenityIds.Contains(a.AmenityId))
            .ToList();

        foreach (var amenity in amenitiesToRemove)
        {
            apartment.Amenities.Remove(amenity);
        }

        _apartmentRepository.Update(apartment);
        await _apartmentRepository.SaveChangesAsync();
    }

    public async Task UpdateApartmentPhotosAsync(Guid apartmentId, List<Microsoft.AspNetCore.Http.IFormFile> photos)
    {
        var apartment = await _apartmentRepository.GetApartmentWithDetailsAsync(apartmentId);
        if (apartment == null)
            throw new ArgumentException("Apartment not found.");

        // remove existing apartment media entries
        var existingMedia = apartment.ApartmentMedia.ToList();
        foreach (var m in existingMedia)
        {
            _apartmentMediumService.DeleteAsync(m.MediaId).Wait();
        }

        // upload new photos
        foreach (var photo in photos)
        {
            var url = await _imageService.UploadImageAsync(photo);
            if (!string.IsNullOrEmpty(url))
            {
                var medium = new ApartmentMedium
                {
                    ApartmentId = apartment.ApartmentId,
                    Url = url,
                    Type = "photo",
                };
                await _apartmentMediumService.CreateAsync(medium);
            }
        }
    }

    public async Task<CreateApartmentResponseDto> CreateApartmentWithPhotosAsync(CreateApartmentRequestDto requestDto, Guid landlordId)
    {
        if (requestDto.photos == null || !requestDto.photos.Any())
        {
            throw new ArgumentException("At least one photo is required to submit an apartment.");
        }

        var apartment = _mapper.Map<Apartment>(requestDto);
        apartment.LandlordId = landlordId;
        apartment.BookingStatus = "available";

        var created = await CreateAsync(apartment);

        foreach (var photo in requestDto.photos)
        {
            var url = await _imageService.UploadImageAsync(photo);

            if (!string.IsNullOrEmpty(url))
            {
                var medium = new ApartmentMedium
                {
                    ApartmentId = created.ApartmentId,
                    Url = url,
                    Type = "photo",
                };
                await _apartmentMediumService.CreateAsync(medium);
            }
        }

        return _mapper.Map<CreateApartmentResponseDto>(created);
    }

    public async Task<ApartmentResponseDto?> GetApartmentWithDetailsAsync(Guid id)
    {
        var apartment = await _apartmentRepository.GetApartmentWithDetailsAsync(id);
        return apartment == null ? null : _mapper.Map<ApartmentResponseDto>(apartment);
    }

    public async Task<ApartmentResponseDto?> GetApartmentWithDetailsResponseAsync(Guid id, Guid? tenantId = null)
    {
        var apartment = await GetApartmentWithDetailsAsync(id);
        if (apartment == null)
        {
            return null;
        }

        await ApplyPriceChangeHistoryAsync(new List<ApartmentResponseDto> { apartment });
        await ApplyWishlistMetadataAsync(apartment, tenantId);
        return apartment;
    }

    private async Task ApplyWishlistMetadataAsync(List<ApartmentResponseDto> apartments, Guid? tenantId)
    {
        if (!tenantId.HasValue || apartments.Count == 0)
        {
            return;
        }

        var apartmentIds = apartments.Select(a => a.ApartmentId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (apartmentIds.Count == 0)
        {
            return;
        }

        var wishlistRows = await _tenantWishlistRepository.GetByTenantAndApartmentIdsAsync(tenantId.Value, apartmentIds);
        var metadataByApartmentId = wishlistRows
            .GroupBy(w => w.ApartmentId)
            .Select(group => group
                .OrderByDescending(w => w.IsFavorite)
                .ThenByDescending(w => w.CreatedAt)
                .First())
            .ToDictionary(
                w => w.ApartmentId,
                w => (w.IsFavorite, w.CollectionId));

        foreach (var apartment in apartments)
        {
            if (metadataByApartmentId.TryGetValue(apartment.ApartmentId, out var metadata))
            {
                apartment.IsFavorite = metadata.IsFavorite;
                apartment.CollectionId = metadata.CollectionId;
            }
        }
    }

    private async Task ApplyWishlistMetadataAsync(ApartmentResponseDto apartment, Guid? tenantId)
    {
        if (!tenantId.HasValue)
        {
            return;
        }

        var wishlistRows = await _tenantWishlistRepository.GetByTenantAndApartmentIdsAsync(tenantId.Value, new[] { apartment.ApartmentId });
        var metadata = wishlistRows
            .OrderByDescending(w => w.IsFavorite)
            .ThenByDescending(w => w.CreatedAt)
            .FirstOrDefault();

        if (metadata == null)
        {
            return;
        }

        apartment.IsFavorite = metadata.IsFavorite;
        apartment.CollectionId = metadata.CollectionId;
    }

    public async Task<bool> ValidateListingDetailsAsync(Guid apartmentId)
    {
        var apartment = await _apartmentRepository.GetApartmentWithDetailsAsync(apartmentId);
        if (apartment == null)
            throw new ArgumentException("Apartment not found.");

        // Validate at least one photo exists
        if (!apartment.ApartmentMedia.Any())
            throw new InvalidOperationException("Apartment must have at least one photo before submission.");

        // Validate at least one amenity exists
        if (!apartment.Amenities.Any())
            throw new InvalidOperationException("Apartment must have at least one amenity before submission.");

        // Validate base price is set
        if (apartment.BasePricePerNight <= 0)
            throw new InvalidOperationException("Apartment must have a valid base price before submission.");

        return true;
    }

    private async Task CreateListingNotificationAsync(
        Guid userId,
        string type,
        string title,
        string message,
        Guid apartmentId,
        bool saveChanges = true)
    {
        await _notificationRepository.AddAsync(new Notification
        {
            NotificationId = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            ReferenceId = apartmentId,
            ReferenceType = "apartment",
            IsRead = false,
            CreatedAt = Common.Utils.VietnamTime.Now
        });

        if (saveChanges)
        {
            await _notificationRepository.SaveChangesAsync();
        }
    }

    public async Task<Apartment> SubmitForReviewAsync(Guid apartmentId, Guid landlordId, SubmitForReviewDto dto)
    {
        var apartment = await _apartmentRepository.GetApartmentWithDetailsAsync(apartmentId);
        if (apartment == null)
            throw new ArgumentException("Apartment not found.");

        // Validate ownership
        if (apartment.LandlordId != landlordId)
            throw new InvalidOperationException("Only the apartment landlord can submit for review.");

        // Validate status is draft
        if (!string.Equals(apartment.Status, "draft", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only draft apartments can be submitted for review.");

        // Validate listing details
        await ValidateListingDetailsAsync(apartmentId);

        // Transition to pending_review
        apartment.Status = "pending_review";
        apartment.BookingStatus = "locked";
        await _apartmentRepository.UpdateListingStatusAsync(
            apartment.ApartmentId,
            apartment.Status,
            apartment.BookingStatus);

        // Notify landlord that the listing was submitted for review
        await CreateListingNotificationAsync(
            landlordId,
            NotificationType.system_announcement.ToString(),
            "Listing submitted for review",
            $"Your listing '{apartment.Title}' has been submitted for review.",
            apartment.ApartmentId);

        // Notify staff users that a new listing is pending review
        var staffUsers = (await _userRepository.FindAsync(u => u.Role.ToLower() == "staff")).ToList();
        if (staffUsers.Count > 0)
        {
            foreach (var staff in staffUsers)
            {
                await CreateListingNotificationAsync(
                    staff.UserId,
                    NotificationType.system_announcement.ToString(),
                    "New listing pending review",
                    $"Listing '{apartment.Title}' has been submitted and is pending review.",
                    apartment.ApartmentId,
                    saveChanges: false);
            }

            await _notificationRepository.SaveChangesAsync();
        }

        return apartment;
    }

    public async Task<Apartment> ApproveListingAsync(Guid apartmentId, Guid adminId, ApproveListingDto dto)
    {
        var apartment = await _apartmentRepository.GetByIdAsync(apartmentId);
        if (apartment == null)
            throw new ArgumentException("Apartment not found.");

        // Validate status is pending_review
        if (!string.Equals(apartment.Status, "pending_review", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only pending_review apartments can be approved or rejected.");

        // Require inspection completion before admin can decide listing status.
        var completedInspections = await _propertyInspectionRepository.FindAsync(i =>
            i.ApartmentId == apartmentId &&
            i.CompletedDate != null);

        if (!completedInspections.Any())
            throw new InvalidOperationException("Listing decision is allowed only after at least one completed property inspection.");

        string type;
        string title;
        string message;

        if (dto.Approved)
        {
            // Approve: transition to posted
            apartment.Status = "posted";
            apartment.BookingStatus = "available";

            type = NotificationType.listing_approved.ToString();
            title = "Listing approved";
            message = $"Your listing '{apartment.Title}' has been approved and is now posted.";
        }
        else
        {
            // Reject: transition to blocked with reason
            if (string.IsNullOrWhiteSpace(dto.RejectionReason))
                throw new InvalidOperationException("A rejection reason must be provided when rejecting a listing.");

            apartment.Status = "blocked";
            apartment.BookingStatus = "locked";

            type = NotificationType.listing_rejected.ToString();
            title = "Listing rejected";
            message = $"Your listing '{apartment.Title}' was rejected. Reason: {dto.RejectionReason}.";
        }

        _apartmentRepository.Update(apartment);
        await _apartmentRepository.SaveChangesAsync();

        await CreateListingNotificationAsync(
            apartment.LandlordId,
            type,
            title,
            message,
            apartment.ApartmentId);

        return apartment;
    }

    public async Task<Apartment> UnpublishApartmentAsync(Guid apartmentId, Guid requesterId, string? reason = null)
    {
        var apartment = await _apartmentRepository.GetByIdAsync(apartmentId);
        if (apartment == null)
            throw new ArgumentException("Apartment not found.");

        // Only allow unpublishing of posted apartments
        if (!string.Equals(apartment.Status, "posted", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only posted apartments can be unpublished.");

        // Transition apartment back to draft status
        apartment.Status = "draft";
        apartment.BookingStatus = "unavailable";

        _apartmentRepository.Update(apartment);
        await _apartmentRepository.SaveChangesAsync();

        // Send notification to landlord
        string title = "Apartment unpublished";
        string message = $"Your apartment '{apartment.Title}' has been unpublished and returned to draft status.";
        if (!string.IsNullOrWhiteSpace(reason))
            message += $" Reason: {reason}";

        await CreateListingNotificationAsync(
            apartment.LandlordId,
            NotificationType.system_announcement.ToString(),
            title,
            message,
            apartment.ApartmentId);

        return apartment;
    }

    public async Task<(IEnumerable<Apartment> Items, int TotalCount)> GetPendingReviewAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, IEnumerable<string>? allowedColumns = null, Dictionary<string, string>? filters = null)
    {
        var effectiveAllowedColumns = new[]
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
            "CreatedAt",
            "MaxOccupants",
            "IsPetAllowed"
        };

        return await _apartmentRepository.GetPendingReviewAsync(page, pageSize, sortBy, sortOrder, search, effectiveAllowedColumns, filters);
    }

    public async Task<(IEnumerable<Apartment> Items, int TotalCount)> GetPendingReviewByLandlordIdAsync(int page, int pageSize, Guid landlordId, string? sortBy = null, string? sortOrder = null, string? search = null, IEnumerable<string>? allowedColumns = null, Dictionary<string, string>? filters = null)
    {
        var effectiveAllowedColumns = new[]
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
            "CreatedAt",
            "MaxOccupants",
            "IsPetAllowed"
        };

        return await _apartmentRepository.GetPendingReviewByLandlordIdAsync(page, pageSize, landlordId, sortBy, sortOrder, search, effectiveAllowedColumns, filters);
    }
}