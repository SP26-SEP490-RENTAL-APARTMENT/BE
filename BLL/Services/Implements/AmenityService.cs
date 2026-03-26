using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements
{
    public class AmenityService : BaseService<Amenity>, IAmenityService
    {
        public AmenityService(IRepository<Amenity> repository) : base(repository)
        {
        }

        public async Task<AmenityResponseDto> CreateAmenityAsync(CreateAmenityRequestDto requestDto)
        {
            var amenity = new Amenity
            {
                NameEn = requestDto.NameEn,
                NameVi = requestDto.NameVi
            };

            await _repository.AddAsync(amenity);
            await _repository.SaveChangesAsync();

            return new AmenityResponseDto
            {
                AmenityId = amenity.AmenityId,
                NameEn = amenity.NameEn,
                NameVi = amenity.NameVi
            };
        }

        public async Task UpdateAmenityAsync(Guid id, UpdateAmenityRequestDto requestDto)
        {
            var amenity = await _repository.GetByIdAsync(id);
            if (amenity == null)
            {
                throw new Exception("Amenity not found");
            }

            amenity.NameEn = requestDto.NameEn;
            amenity.NameVi = requestDto.NameVi;

            _repository.Update(amenity);
            await _repository.SaveChangesAsync();
        }

        public async Task<AmenityResponseDto?> GetAmenityByIdAsync(Guid id)
        {
            var amenity = await _repository.GetByIdAsync(id);
            if (amenity == null) return null;

            return new AmenityResponseDto
            {
                AmenityId = amenity.AmenityId,
                NameEn = amenity.NameEn,
                NameVi = amenity.NameVi
            };
        }

        public async Task<(IEnumerable<AmenityResponseDto> Items, int TotalCount)> GetAllAmenitiesAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null)
        {
            var allowedColumns = new[] { "AmenityId", "NameEn", "NameVi" };
            var result = await _repository.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters, allowedColumns);

            var items = result.Items.Select(a => new AmenityResponseDto
            {
                AmenityId = a.AmenityId,
                NameEn = a.NameEn,
                NameVi = a.NameVi
            });

            return (items, result.TotalCount);
        }
    }
}