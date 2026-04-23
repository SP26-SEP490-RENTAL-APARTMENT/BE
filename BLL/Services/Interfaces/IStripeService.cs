using Common.DTOs;

namespace BLL.Services.Interfaces;

public interface IStripeService
{
    Task<StripeCheckoutResponseDto> CreateCheckoutSessionAsync(StripeCheckoutRequestDto request, CancellationToken cancellationToken = default);

    Task<string> RefundCheckoutSessionAsync(string checkoutSessionId, long amount, CancellationToken cancellationToken = default);
}
