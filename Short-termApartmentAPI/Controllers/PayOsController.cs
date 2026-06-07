using BLL.Services.Interfaces;
using Common.Enums;
using DAL.Repository.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PayOS;
using PayOS.Exceptions;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace Short_termApartmentAPI.Controllers;

[ApiController]
[Route("api/payments/webhook")]
public class PayOsController : ControllerBase
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> DepositWebhookLocks = new();

    private readonly IPayOsService _payOsService;
    private readonly IRepository<DAL.Models.Payment> _paymentRepository;
    private readonly IRepository<DAL.Models.BookingCheckTime> _bookingCheckTimeRepository;
    private readonly IRepository<DAL.Models.Booking> _bookingRepository;
    private readonly IRepository<DAL.Models.BookingCheckTimeStateEvent> _checkTimeEventRepository;
    private readonly IRepository<DAL.Models.User> _userRepository;
    private readonly PayOS.PayOSClient? _paymentSdkClient;
    private readonly IBookingService _bookingService;
    private readonly IPayOSPayoutService _payOSPayoutService;
    private readonly ILogger<PayOsController> _logger;

    public PayOsController(
        IPayOsService payOsService,
        IRepository<DAL.Models.Payment> paymentRepository,
        IRepository<DAL.Models.BookingCheckTime> bookingCheckTimeRepository,
        IRepository<DAL.Models.Booking> bookingRepository,
        IRepository<DAL.Models.BookingCheckTimeStateEvent> checkTimeEventRepository,
        IRepository<DAL.Models.User> userRepository,
        ILogger<PayOsController> logger,
        IBookingService bookingService,
        IPayOSPayoutService payOSPayoutService,
        [FromKeyedServices("OrderClient")] PayOSClient? paymentSdkClient = null)
    {
        _payOsService = payOsService;
        _paymentRepository = paymentRepository;
        _bookingCheckTimeRepository = bookingCheckTimeRepository;
        _bookingRepository = bookingRepository;
        _checkTimeEventRepository = checkTimeEventRepository;
        _userRepository = userRepository;
        _logger = logger;
        _paymentSdkClient = paymentSdkClient;
        _bookingService = bookingService;
        _payOSPayoutService = payOSPayoutService;
    }

    [HttpPost("payos")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> PayOsWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

        // Try common header names used for signatures
        var signatureHeader = Request.Headers["PayOS-Signature"].FirstOrDefault()
                              ?? Request.Headers["X-PayOS-Signature"].FirstOrDefault()
                              ?? Request.Headers["Payos-Signature"].FirstOrDefault()
                              ?? Request.Headers["X-Payos-Signature"].FirstOrDefault()
                              ?? string.Empty;

        _logger.LogInformation("[PayOS Webhook] Received payload. Length: {PayloadLength}, HasSignature: {HasSignature}", json?.Length ?? 0, !string.IsNullOrEmpty(signatureHeader));
        _logger.LogDebug("[PayOS Webhook] Payload: {Payload}", json);

        try
        {
            string? reference = null;
            string? paymentLinkId = null;
            string? payerAccountNumber = null;
            string? payerAccountName = null;
            string? payerBankName = null;
            string? payerBankBin = null;
            string? receivingAccountNumber = null;
            string? receivingBankBin = null;

            if (_paymentSdkClient != null)
            {
                // Prefer SDK-based verification
                _logger.LogInformation("[PayOS Webhook] Using SDK client for verification");
                var webhookModel = System.Text.Json.JsonSerializer.Deserialize<PayOS.Models.Webhooks.Webhook>(json ?? string.Empty);
                if (webhookModel == null)
                {
                    _logger.LogError("[PayOS Webhook] Failed to deserialize webhook model");
                    return BadRequest(new { error = "Failed to deserialize webhook data" });
                }

                _logger.LogInformation("[PayOS Webhook] Webhook model deserialized. Code: {Code}, HasSignature: {HasSig}",
                    webhookModel.Code, !string.IsNullOrEmpty(webhookModel.Signature));

                try
                {
                    var webhookData = await _paymentSdkClient.Webhooks.VerifyAsync(webhookModel!).ConfigureAwait(false);
                    if (webhookData == null)
                    {
                        _logger.LogError("[PayOS Webhook] SDK verification failed (null result)");
                        return BadRequest(new { error = "Invalid webhook (SDK verification failed)" });
                    }

                    _logger.LogInformation("[PayOS Webhook] SDK verification successful");
                    var verifiedJson = JsonSerializer.Serialize(webhookData);
                    using (var doc = JsonDocument.Parse(verifiedJson))
                    {
                        var root = GetPayOsDataRoot(doc.RootElement);
                        reference = GetJsonString(root, "reference", "Reference");
                        paymentLinkId = GetJsonString(root, "paymentLinkId", "PaymentLinkId", "payment_link_id");
                        payerAccountNumber = GetJsonString(root, "counterAccountNumber", "CounterAccountNumber");
                        payerAccountName = GetJsonString(root, "counterAccountName", "CounterAccountName");
                        payerBankName = GetJsonString(root, "counterAccountBankName", "CounterAccountBankName");
                        payerBankBin = GetJsonString(root, "counterAccountBankId", "CounterAccountBankId");
                        receivingAccountNumber = GetJsonString(root, "accountNumber", "AccountNumber");
                        receivingBankBin = GetJsonString(root, "bin", "Bin");
                    }
                }
                catch (WebhookException wex)
                {
                    _logger.LogWarning(wex, "[PayOS Webhook] SDK verification failed: {Message}. Attempting fallback verification", wex.Message);

                    // Fallback to service-based verification if SDK fails
                    if (string.IsNullOrEmpty(json))
                    {
                        _logger.LogError("[PayOS Webhook] Received empty payload for fallback");
                        return BadRequest(new { error = "Empty payload" });
                    }

                    var verified = _payOsService.VerifyWebhookSignature(json, signatureHeader);
                    if (!verified)
                    {
                        _logger.LogError("[PayOS Webhook] Service-based signature verification also failed");
                        return BadRequest(new { error = "Invalid signature" });
                    }

                    _logger.LogInformation("[PayOS Webhook] Service-based verification successful (fallback)");
                    using (var doc = JsonDocument.Parse(json))
                    {
                        var root = GetPayOsDataRoot(doc.RootElement);
                        reference = GetJsonString(root, "reference", "Reference");
                        paymentLinkId = GetJsonString(root, "paymentLinkId", "PaymentLinkId", "payment_link_id");
                        payerAccountNumber = GetJsonString(root, "counterAccountNumber", "CounterAccountNumber");
                        payerAccountName = GetJsonString(root, "counterAccountName", "CounterAccountName");
                        payerBankName = GetJsonString(root, "counterAccountBankName", "CounterAccountBankName");
                        payerBankBin = GetJsonString(root, "counterAccountBankId", "CounterAccountBankId");
                        receivingAccountNumber = GetJsonString(root, "accountNumber", "AccountNumber");
                        receivingBankBin = GetJsonString(root, "bin", "Bin");
                    }
                }
            }
            else
            {
                _logger.LogInformation("[PayOS Webhook] Using service-based signature verification");
                if (string.IsNullOrEmpty(json))
                {
                    _logger.LogError("[PayOS Webhook] Received empty payload");
                    return BadRequest(new { error = "Empty payload" });
                }

                var verified = _payOsService.VerifyWebhookSignature(json, signatureHeader);
                if (!verified)
                {
                    _logger.LogError("[PayOS Webhook] Signature verification failed. Header: {SignatureHeader}", signatureHeader);
                    return BadRequest(new { error = "Invalid signature" });
                }

                _logger.LogInformation("[PayOS Webhook] Signature verification successful");
                using (var doc = JsonDocument.Parse(json))
                {
                    var root = GetPayOsDataRoot(doc.RootElement);
                    reference = GetJsonString(root, "reference", "Reference");
                    paymentLinkId = GetJsonString(root, "paymentLinkId", "PaymentLinkId", "payment_link_id");
                    payerAccountNumber = GetJsonString(root, "counterAccountNumber", "CounterAccountNumber");
                    payerAccountName = GetJsonString(root, "counterAccountName", "CounterAccountName");
                    payerBankName = GetJsonString(root, "counterAccountBankName", "CounterAccountBankName");
                    payerBankBin = GetJsonString(root, "counterAccountBankId", "CounterAccountBankId");
                    receivingAccountNumber = GetJsonString(root, "accountNumber", "AccountNumber");
                    receivingBankBin = GetJsonString(root, "bin", "Bin");
                }
            }


            _logger.LogInformation("[PayOS Webhook] Extracted reference: {Reference}, paymentLinkId: {PaymentLinkId}", reference, paymentLinkId);

            var payment = (await _paymentRepository.FindAsync(p => p.TransactionId == reference || p.TransactionId == paymentLinkId)).FirstOrDefault();
            if (payment != null)
            {
                _logger.LogInformation("[PayOS Webhook] Found payment record. PaymentId: {PaymentId}, RelatedEntityId: {RelatedEntityId}", payment.PaymentId, payment.RelatedEntityId);

                // Serialize concurrent webhooks for deposit/upfront payments on the same booking so the
                // "already paid" check and the status write are atomic within this process. Without this,
                // two simultaneous webhooks both read alreadyPaid=false, both write status=success, and
                // neither is caught by the duplicate guard below.
                bool isDuplicateDeposit = false;
                if (payment.RelatedEntityId.HasValue &&
                    string.Equals(payment.RelatedEntityType, Common.Enums.PaymentRelatedEntityType.booking.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    var paymentType = payment.PaymentType?.Trim().ToLowerInvariant();
                    if (paymentType == Common.Enums.PaymentTypes.deposit.ToString() ||
                        paymentType == Common.Enums.PaymentTypes.upfront.ToString())
                    {
                        var bookingLock = DepositWebhookLocks.GetOrAdd(payment.RelatedEntityId.Value, _ => new SemaphoreSlim(1, 1));
                        await bookingLock.WaitAsync();
                        try
                        {
                            var alreadyPaid = (await _paymentRepository.FindAsync(p =>
                                p.PaymentId != payment.PaymentId &&
                                p.RelatedEntityId == payment.RelatedEntityId &&
                                p.RelatedEntityType == payment.RelatedEntityType &&
                                (p.PaymentType == Common.Enums.PaymentTypes.deposit.ToString() ||
                                 p.PaymentType == Common.Enums.PaymentTypes.upfront.ToString()) &&
                                p.Status == Common.Enums.PaymentStatus.success.ToString())).Any();

                            if (alreadyPaid)
                            {
                                isDuplicateDeposit = true;
                                _logger.LogWarning(
                                    "[PayOS Webhook] Duplicate deposit payment detected for booking {BookingId}. Voiding payment {PaymentId} and initiating automatic refund.",
                                    payment.RelatedEntityId, payment.PaymentId);

                                payment.Status = Common.Enums.PaymentStatus.refunded.ToString();
                                payment.Notes = "Voided: a successful deposit payment already exists for this booking. Automatic refund initiated.";
                                _paymentRepository.Update(payment);
                                await _paymentRepository.SaveChangesAsync();
                            }
                            else
                            {
                                // Mark success inside the lock so a racing webhook sees this write
                                // when it acquires the lock and runs its own alreadyPaid check.
                                payment.Status = "success";
                                payment.SettlementStatus = "settled";
                                payment.PaidAt = Common.Utils.VietnamTime.Now;
                                payment.PayerAccountNumber = payerAccountNumber;
                                payment.PayerBankName = payerBankName;
                                payment.PayerBankBin = payerBankBin;
                                payment.ReceivingAccountNumber = receivingAccountNumber;
                                payment.ReceivingBankBin = receivingBankBin;
                                _paymentRepository.Update(payment);
                                await _paymentRepository.SaveChangesAsync();
                            }
                        }
                        finally
                        {
                            bookingLock.Release();
                        }

                        if (isDuplicateDeposit)
                        {
                            if (!string.IsNullOrWhiteSpace(payerAccountNumber) &&
                                !string.IsNullOrWhiteSpace(payerBankBin) &&
                                payment.Amount >= 2000)
                            {
                                try
                                {
                                    var refundReference = $"refund-dup-{payment.PaymentId:N}";
                                    var refundAmount = (long)Math.Round(payment.Amount);
                                    var receiverName = payerAccountName ?? "Tenant";
                                    var result = await _payOSPayoutService.CreateBankPayoutAsync(
                                        receiverName, payerAccountNumber, payerBankBin, refundAmount, refundReference);

                                    _logger.LogInformation(
                                        "[PayOS Webhook] Duplicate-payment refund payout created. PaymentId={PaymentId} PayoutId={PayoutId} ResultCode={ResultCode}",
                                        payment.PaymentId, result.PayoutId, result.ResultCode);
                                }
                                catch (Exception refundEx)
                                {
                                    _logger.LogError(refundEx,
                                        "[PayOS Webhook] Failed to create automatic refund payout for duplicate payment {PaymentId}. Manual refund required.",
                                        payment.PaymentId);
                                }
                            }
                            else
                            {
                                _logger.LogWarning(
                                    "[PayOS Webhook] Cannot auto-refund duplicate payment {PaymentId}: missing payer bank details or amount below minimum (AccountNumber={AccountNumber}, BankBin={BankBin}, Amount={Amount}).",
                                    payment.PaymentId, payerAccountNumber, payerBankBin, payment.Amount);
                            }

                            return Ok(new { success = true });
                        }
                    }
                    else
                    {
                        // Non-deposit booking payment — mark success outside any lock (no duplicate risk)
                        payment.Status = "success";
                        payment.SettlementStatus = "settled";
                        payment.PaidAt = Common.Utils.VietnamTime.Now;
                        payment.PayerAccountNumber = payerAccountNumber;
                        payment.PayerBankName = payerBankName;
                        payment.PayerBankBin = payerBankBin;
                        payment.ReceivingAccountNumber = receivingAccountNumber;
                        payment.ReceivingBankBin = receivingBankBin;
                        _paymentRepository.Update(payment);
                        await _paymentRepository.SaveChangesAsync();
                    }
                }
                else
                {
                    payment.Status = "success";
                    payment.SettlementStatus = "settled";
                    payment.PaidAt = Common.Utils.VietnamTime.Now;
                    payment.PayerAccountNumber = payerAccountNumber;
                    payment.PayerBankName = payerBankName;
                    payment.PayerBankBin = payerBankBin;
                    payment.ReceivingAccountNumber = receivingAccountNumber;
                    payment.ReceivingBankBin = receivingBankBin;
                    _paymentRepository.Update(payment);
                    await _paymentRepository.SaveChangesAsync();
                }

                if (payment.RelatedEntityId.HasValue &&
                    (string.Equals(payment.RelatedEntityType, PaymentRelatedEntityType.booking.ToString(), StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(payment.RelatedEntityType, "booking_check_time", StringComparison.OrdinalIgnoreCase)))
                {
                    await UpdateUserBankProfileFromBookingAsync(payment.RelatedEntityId.Value, payerAccountName, payerAccountNumber, payerBankName, payerBankBin, payment.PaymentId);
                }

                if (payment.RelatedEntityId.HasValue &&
                    string.Equals(payment.RelatedEntityType, PaymentRelatedEntityType.booking.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    var bookingId = payment.RelatedEntityId.Value;
                    var paymentType = payment.PaymentType?.Trim().ToLowerInvariant();

                    try
                    {
                        if (paymentType == Common.Enums.PaymentTypes.balance.ToString())
                        {
                            await _bookingService.MarkBalancePaidAsync(bookingId);
                        }
                        else if (paymentType == Common.Enums.PaymentTypes.deposit.ToString() ||
                                 paymentType == Common.Enums.PaymentTypes.upfront.ToString())
                        {
                            await _bookingService.MarkDepositPaidAsync(bookingId, skipConflictCheck: true);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[PayOS Webhook] Failed to apply booking payment status for booking {BookingId}", bookingId);
                    }

                    var checkTime = (await _bookingCheckTimeRepository.FindAsync(ct => ct.BookingId == bookingId)).FirstOrDefault();
                    if (checkTime != null)
                    {
                        _logger.LogInformation("[PayOS Webhook] Updating check-time for booking {BookingId}", bookingId);

                        checkTime.FeeSettlementStatus = "paid";
                        checkTime.FeeSettledAt = Common.Utils.VietnamTime.Now;
                        checkTime.FeeDueAt = null;
                        checkTime.ClaimLockedAt ??= Common.Utils.VietnamTime.Now;
                        checkTime.ClaimStatus = "paid";
                        _bookingCheckTimeRepository.Update(checkTime);
                        await _bookingCheckTimeRepository.SaveChangesAsync();

                        try
                        {
                            var ev = new DAL.Models.BookingCheckTimeStateEvent
                            {
                                EventId = Guid.NewGuid(),
                                BookingId = bookingId,
                                CheckTimeId = checkTime.CheckTimeId,
                                EventType = "payment_settled",
                                EventData = JsonSerializer.Serialize(new { PaymentId = payment.PaymentId, TransactionId = payment.TransactionId, Amount = payment.Amount, Method = payment.Method }),
                                CreatedBy = null,
                                CreatedAt = Common.Utils.VietnamTime.Now
                            };
                            await _checkTimeEventRepository.AddAsync(ev);
                            await _checkTimeEventRepository.SaveChangesAsync();

                            try
                            {
                                await _bookingService.TryFinalizeLandlordFundsReleaseAsync(bookingId, "payos_webhook");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "[PayOS Webhook] Failed to finalize landlord fund release for booking {BookingId}", bookingId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "[PayOS Webhook] Failed to create state event for booking {BookingId}", bookingId);
                        }

                        var booking = await _bookingRepository.GetByIdAsync(bookingId);
                        if (booking != null && string.Equals(booking.Status, "disputed", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("[PayOS Webhook] Updating booking {BookingId} status from 'disputed' to 'completed'", bookingId);
                            booking.Status = "completed";
                            _bookingRepository.Update(booking);
                            await _bookingRepository.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        _logger.LogWarning("[PayOS Webhook] Check-time not found for booking {BookingId}", bookingId);
                    }
                }
                else if (payment.RelatedEntityId.HasValue &&
                         string.Equals(payment.RelatedEntityType, "booking_check_time", StringComparison.OrdinalIgnoreCase))
                {
                    var bookingId = payment.RelatedEntityId.Value;
                    var checkTime = (await _bookingCheckTimeRepository.FindAsync(ct => ct.BookingId == bookingId)).FirstOrDefault();

                    if (checkTime != null)
                    {
                        _logger.LogInformation("[PayOS Webhook] Updating check-time fee for booking {BookingId}", bookingId);

                        checkTime.FeeSettlementStatus = "paid";
                        checkTime.FeeSettledAt = Common.Utils.VietnamTime.Now;
                        checkTime.FeeDueAt = null;
                        checkTime.ClaimLockedAt ??= Common.Utils.VietnamTime.Now;
                        checkTime.ClaimStatus = "paid";
                        _bookingCheckTimeRepository.Update(checkTime);
                        await _bookingCheckTimeRepository.SaveChangesAsync();

                        try
                        {
                            var ev = new DAL.Models.BookingCheckTimeStateEvent
                            {
                                EventId = Guid.NewGuid(),
                                BookingId = bookingId,
                                CheckTimeId = checkTime.CheckTimeId,
                                EventType = "payment_settled",
                                EventData = JsonSerializer.Serialize(new { PaymentId = payment.PaymentId, TransactionId = payment.TransactionId, Amount = payment.Amount, Method = payment.Method }),
                                CreatedBy = null,
                                CreatedAt = Common.Utils.VietnamTime.Now
                            };
                            await _checkTimeEventRepository.AddAsync(ev);
                            await _checkTimeEventRepository.SaveChangesAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "[PayOS Webhook] Failed to create check-time state event for booking {BookingId}", bookingId);
                        }

                        try
                        {
                            await _bookingService.TryFinalizeLandlordFundsReleaseAsync(bookingId, "payos_webhook");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "[PayOS Webhook] Failed to finalize landlord fund release for booking {BookingId}", bookingId);
                        }

                        var booking = await _bookingRepository.GetByIdAsync(bookingId);
                        if (booking != null && string.Equals(booking.Status, "disputed", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("[PayOS Webhook] Updating booking {BookingId} status from 'disputed' to 'completed'", bookingId);
                            booking.Status = "completed";
                            _bookingRepository.Update(booking);
                            await _bookingRepository.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        _logger.LogWarning("[PayOS Webhook] Check-time not found for booking {BookingId}", bookingId);
                    }
                }
                else
                {
                    _logger.LogInformation("[PayOS Webhook] Payment has no related booking entity");
                }

                _logger.LogInformation("[PayOS Webhook] Successfully processed payment");
            }
            else
            {
                _logger.LogWarning("[PayOS Webhook] No payment found with reference {Reference} or paymentLinkId {PaymentLinkId}", reference, paymentLinkId);
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PayOS Webhook] Error processing webhook: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    private static string? GetJsonString(JsonElement root, params string[] names)
    {
        foreach (var n in names)
        {
            if (root.TryGetProperty(n, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }

        return null;
    }

    private static JsonElement GetPayOsDataRoot(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Object)
        {
            return data;
        }
        return root;
    }

    private async Task UpdateUserBankProfileFromBookingAsync(
        Guid bookingId,
        string? payerAccountName,
        string? payerAccountNumber,
        string? payerBankName,
        string? payerBankBin,
        Guid paymentId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking == null)
        {
            return;
        }

        var tenantUser = await _userRepository.GetByIdAsync(booking.TenantId);
        if (tenantUser == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(payerAccountName))
        {
            tenantUser.BankAccountHolderName = payerAccountName;
        }

        if (!string.IsNullOrWhiteSpace(payerAccountNumber))
        {
            tenantUser.BankAccountNumber = payerAccountNumber;
        }

        if (!string.IsNullOrWhiteSpace(payerBankName))
        {
            tenantUser.BankName = payerBankName;
        }

        if (!string.IsNullOrWhiteSpace(payerBankBin))
        {
            tenantUser.BankBin = payerBankBin;
        }

        _userRepository.Update(tenantUser);
        await _userRepository.SaveChangesAsync();

        _logger.LogInformation("[PayOS Webhook] Updated bank profile for user {UserId} from successful payment {PaymentId}", tenantUser.UserId, paymentId);
    }
}
