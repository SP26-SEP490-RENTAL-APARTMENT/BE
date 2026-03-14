using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public sealed class PropertyInspectionService(IRepository<PropertyInspection> repository)
    : BaseService<PropertyInspection>(repository), IPropertyInspectionService
{
}
