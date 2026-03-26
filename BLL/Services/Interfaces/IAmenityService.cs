using DAL.Models;
using Common.DTOs;

namespace BLL.Services.Interfaces
{
    public interface IAmenityService : IBaseService<Amenity>
    {
        Task<AmenityResponseDto> CreateAmenityAsync(CreateAmenityRequestDto requestDto);
        Task UpdateAmenityAsync(Guid id, UpdateAmenityRequestDto requestDto);
        Task<AmenityResponseDto?> GetAmenityByIdAsync(Guid id);
        Task<(IEnumerable<AmenityResponseDto> Items, int TotalCount)> GetAllAmenitiesAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null);
    }
}