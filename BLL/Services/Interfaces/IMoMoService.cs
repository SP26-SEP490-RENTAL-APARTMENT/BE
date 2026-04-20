using Common.DTOs;

namespace BLL.Services.Interfaces;

public interface IMomoService
{
    Task<MomoCreatePaymentResponse> CreateWalletPaymentAsync(MomoCreatePaymentRequest request, CancellationToken cancellationToken = default);

    Task<MomoQueryPaymentResponse> QueryPaymentStatusAsync(MomoQueryPaymentRequest request, CancellationToken cancellationToken = default);

    Task<MomoDisbursementResponse> VerifyWalletAsync(MomoVerifyWalletRequest request, CancellationToken cancellationToken = default);

    Task<MomoDisbursementResponse> CreateDisbursementAsync(MomoDisbursementRequest request, CancellationToken cancellationToken = default);

    Task<MomoQueryDisbursementResponse> QueryDisbursementStatusAsync(MomoQueryDisbursementRequest request, CancellationToken cancellationToken = default);

    bool ValidateDisbursementIpnSignature(string requestBody);
}
