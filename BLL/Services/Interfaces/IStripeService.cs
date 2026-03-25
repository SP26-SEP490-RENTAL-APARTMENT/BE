using Common.DTOs;

namespace BLL.Services.Interfaces;

public interface IStripeService
{
    Task<StripeCheckoutResponseDto> CreateCheckoutSessionAsync(StripeCheckoutRequestDto request, CancellationToken cancellationToken = default);
}
