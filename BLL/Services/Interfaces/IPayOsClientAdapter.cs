using System.Threading.Tasks;

namespace BLL.Services.Interfaces
{
    public interface IPayOsClientAdapter
    {
        Task<PayOS.Models.V2.PaymentRequests.CreatePaymentLinkResponse> CreatePaymentLinkAsync(PayOS.Models.V2.PaymentRequests.CreatePaymentLinkRequest request);
        Task<object?> GetPaymentRequestAsync(string id);
        Task<object?> VerifyWebhookAsync(PayOS.Models.Webhooks.Webhook webhook);
    }
}
