using Common.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Short_termApartmentAPI.Services;

public interface IMomoWebhookService
{
    Task<IActionResult> HandleIpnAsync(HttpContext httpContext, string body);

    Task<IActionResult> ReconcileBookingPaymentAsync(ReconcileBookingPaymentRequestDto dto);

    Task<IActionResult> ReconcileSubscriptionPaymentAsync(ReconcileLandlordSubscriptionPaymentRequestDto dto);

    Task<IActionResult> HandleDisbursementIpnAsync(string body);
}
