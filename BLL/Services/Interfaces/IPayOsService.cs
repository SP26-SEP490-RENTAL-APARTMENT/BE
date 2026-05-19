using Common.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace BLL.Services.Interfaces
{
    public interface IPayOsService
    {
        Task<PayOsCreatePaymentResponse> CreateCheckoutAsync(PayOsCreatePaymentRequest request, CancellationToken cancellationToken = default);
        Task<string> QueryPaymentStatusAsync(string orderId, CancellationToken cancellationToken = default);
        bool VerifyWebhookSignature(string requestBody, string signatureHeader);
    }
}
