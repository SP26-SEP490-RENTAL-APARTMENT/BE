using Common.DTOs;

namespace BLL.Services.Interfaces;

public interface IMomoService
{
    Task<MomoCreatePaymentResponse> CreateWalletPaymentAsync(MomoCreatePaymentRequest request, CancellationToken cancellationToken = default);
}
