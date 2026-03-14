using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public sealed class PaymentService(IRepository<Payment> repository)
    : BaseService<Payment>(repository), IPaymentService
{
}
