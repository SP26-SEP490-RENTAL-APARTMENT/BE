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

    public ApartmentService(
        IApartmentRepository repository,
        IImageService imageService,
        IApartmentMediumService apartmentMediumService,
        IMapper mapper)
        : base(repository)
    {
        _apartmentRepository = repository;
        _imageService = imageService;
        _apartmentMediumService = apartmentMediumService;
        _mapper = mapper;
    }

    public async Task<CreateApartmentResponseDto> CreateApartmentWithPhotosAsync(CreateApartmentRequestDto requestDto, Guid landlordId)
    {
        if (requestDto.Photos == null || !requestDto.Photos.Any())
        {
            throw new ArgumentException("At least one photo is required to submit an apartment.");
        }

        var apartment = _mapper.Map<Apartment>(requestDto);
        apartment.LandlordId = landlordId;

        var created = await CreateAsync(apartment);

        foreach (var photo in requestDto.Photos)
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
}
