using Common.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Short_termApartmentAPI.Services;

public interface IMomoWebhookService
{
    Task<IActionResult> HandleIpnAsync(HttpContext httpContext, string body);

    Task<IActionResult> HandleWebhookListenerAsync(HttpContext httpContext, string body);

    Task ProcessQueuedIpnEnvelopeAsync(string envelopeRaw, CancellationToken cancellationToken = default);

    Task ProcessQueriedPaymentAsync(string responseBody, CancellationToken cancellationToken = default);

    Task<IActionResult> ReconcileBookingPaymentAsync(ReconcileBookingPaymentRequestDto dto);

    Task<IActionResult> ReconcileSubscriptionPaymentAsync(ReconcileLandlordSubscriptionPaymentRequestDto dto);

    Task<IActionResult> HandleDisbursementIpnAsync(string body);
}
