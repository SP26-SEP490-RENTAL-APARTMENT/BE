using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public sealed class SmartPricingHistoryService(IRepository<SmartPricingHistory> repository)
    : BaseService<SmartPricingHistory>(repository), ISmartPricingHistoryService
{
}
