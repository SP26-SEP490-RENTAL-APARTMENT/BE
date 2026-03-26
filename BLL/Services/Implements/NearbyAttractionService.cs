using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public class NearbyAttractionService : BaseService<NearbyAttraction>, INearbyAttractionService
{
    public NearbyAttractionService(IRepository<NearbyAttraction> repository) : base(repository)
    {
    }

    public override async Task<(IEnumerable<NearbyAttraction> Items, int TotalCount)> GetAllAsync(
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
            "AttractionId",
            "NameEn",
            "NameVi",
            "Type",
            "Address",
            "City"
        };

        return await base.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters, effectiveAllowedColumns);
    }
}
