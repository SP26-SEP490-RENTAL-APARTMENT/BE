using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public class InspectionPhotoService : BaseService<InspectionPhoto>, IInspectionPhotoService
{
    public InspectionPhotoService(IRepository<InspectionPhoto> repository) : base(repository)
    {
    }
}
