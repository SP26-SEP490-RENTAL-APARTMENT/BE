using DAL.Models;
using Common.DTOs;

namespace BLL.Services.Interfaces;

public interface IApartmentService : IBaseService<Apartment>
{
    Task<CreateApartmentResponseDto> CreateApartmentWithPhotosAsync(CreateApartmentRequestDto requestDto, Guid landlordId);
    Task<ApartmentResponseDto?> GetApartmentWithDetailsAsync(Guid id);
    Task AddAmenitiesAsync(Guid apartmentId, List<Guid> amenityIds);
    Task UpdateApartmentPhotosAsync(Guid apartmentId, List<Microsoft.AspNetCore.Http.IFormFile> photos);
}
