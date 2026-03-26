using AutoMapper;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public class ApartmentService : BaseService<Apartment>, IApartmentService
{
    private readonly IImageService _imageService;
    private readonly IApartmentMediumService _apartmentMediumService;
    private readonly IMapper _mapper;

    private readonly IApartmentRepository _apartmentRepository;
    private readonly IAmenityRepository _amenityRepository;

    public ApartmentService(
        IApartmentRepository repository,
        IAmenityRepository amenityRepository,
        IImageService imageService,
        IApartmentMediumService apartmentMediumService,
        IMapper mapper)
        : base(repository)
    {
        _apartmentRepository = repository;
        _amenityRepository = amenityRepository;
        _imageService = imageService;
        _apartmentMediumService = apartmentMediumService;
        _mapper = mapper;
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
            "CreatedAt"
        };

        return await base.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters, effectiveAllowedColumns);
    }

    public async Task AddAmenitiesAsync(Guid apartmentId, List<Guid> amenityIds)
    {
        var apartment = await _apartmentRepository.GetApartmentWithDetailsAsync(apartmentId);
        if (apartment == null)
        {
            throw new ArgumentException("Apartment not found.");
        }

        foreach (var amenityId in amenityIds)
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
        _apartmentRepository.Update(apartment);
        await _apartmentRepository.SaveChangesAsync();

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

        if (dto.Approved)
        {
            // Approve: transition to posted
            apartment.Status = "posted";
        }
        else
        {
            // Reject: transition to blocked with reason
            if (string.IsNullOrWhiteSpace(dto.RejectionReason))
                throw new InvalidOperationException("A rejection reason must be provided when rejecting a listing.");

            apartment.Status = "blocked";
        }

        _apartmentRepository.Update(apartment);
        await _apartmentRepository.SaveChangesAsync();

        return apartment;
    }
}
