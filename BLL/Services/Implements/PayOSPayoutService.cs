using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BLL.Services.Interfaces;
using Microsoft.Extensions.Logging;
using PayOS;
using PayOS.Exceptions;
using PayOS.Models.V1.Payouts;

namespace BLL.Services.Implements;

public class PayOSPayoutService : IPayOSPayoutService
{
    private const long MinPayoutAmount = 2000;

    private static readonly HashSet<string> SuccessStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "APPROVED",
        "COMPLETED",
        "SUCCEEDED",
        "SUCCESS"
    };

    private static readonly HashSet<string> PendingStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "PENDING",
        "PROCESSING",
        "DRAFT",
        "REQUESTED",
        "IN_PROGRESS"
    };

    private static readonly HashSet<string> FailedStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "FAILED",
        "REJECTED",
        "CANCELLED",
        "CANCELED",
        "EXPIRED"
    };

    private readonly PayOSClient? _client;
    private readonly ILogger<PayOSPayoutService>? _logger;

    public PayOSPayoutService()
    {
        _client = null;
        _logger = null;
    }

    public PayOSPayoutService(PayOSClient client, ILogger<PayOSPayoutService> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger;
    }

    public async Task<PayOSPayoutResult> CreateBankPayoutAsync(string receiverName, string accountOrCard, string bankCode, long amount, string reference, CancellationToken cancellationToken = default)
    {
        if (amount < MinPayoutAmount)
        {
            throw new ArgumentException($"Payment amount must be at least {MinPayoutAmount}.", nameof(amount));
        }

        var payoutRequest = new PayoutRequest
        {
            ReferenceId = reference,
            Amount = amount,
            Description = "Landlord payout",
            ToBin = bankCode,
            ToAccountNumber = accountOrCard
        };

        var requestJson = JsonSerializer.Serialize(payoutRequest);
        _logger?.LogInformation("Creating PayOS bank payout: Reference={Reference} Amount={Amount} Receiver={Receiver}", reference, amount, receiverName);
        _logger?.LogDebug("PayOS payout request payload: {RequestJson}", requestJson);

        if (_client == null) throw new InvalidOperationException("PayOS client is not configured.");

        Payout payoutResponse;
        try
        {
            payoutResponse = await _client.Payouts.CreateAsync(payoutRequest);
        }
        catch (ForbiddenException ex)
        {
            var details = ExtractProviderErrorDetails(ex);

            _logger?.LogWarning(
            ex,
            "PayOS forbidden while creating payout. Reference={Reference} StatusCode={StatusCode} ProviderCode={ProviderCode} ProviderMessage={ProviderMessage} CorrelationId={CorrelationId}",
            reference,
            details.StatusCode ?? 403,
            details.ProviderCode,
            details.ProviderMessage,
            details.CorrelationId);

            throw new InvalidOperationException("Payout provider rejected request due to insufficient permission.");
        }
        catch (ApiException ex)
        {
            var details = ExtractProviderErrorDetails(ex);

            _logger?.LogError(
            ex,
            "PayOS API error while creating payout. Reference={Reference} StatusCode={StatusCode} ProviderCode={ProviderCode} ProviderMessage={ProviderMessage} CorrelationId={CorrelationId}",
            reference,
            details.StatusCode,
            details.ProviderCode,
            details.ProviderMessage,
            details.CorrelationId);

            throw MapApiExceptionToDomainException(details, ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "PayOS CreateAsync failed for Reference={Reference}", reference);
            throw new InvalidOperationException("Payout provider request failed unexpectedly.", ex);
        }



        var responseJson = JsonSerializer.Serialize(payoutResponse, new JsonSerializerOptions { WriteIndented = false });
        _logger?.LogDebug("PayOS payout response payload: {ResponseJson}", responseJson);

        var approvalState = payoutResponse.ApprovalState.ToString();
        var transactionState = GetPrimaryTransactionState(payoutResponse.Transactions);
        var resultCode = MapProviderStatusToResultCode(approvalState, transactionState);

        var payoutIdVal = !string.IsNullOrEmpty(payoutResponse.Id) ? payoutResponse.Id : payoutResponse.ReferenceId;
        string? transId = null;
        if (payoutResponse.Transactions != null && payoutResponse.Transactions.Count > 0)
        {
            transId = payoutResponse.Transactions[0].Id;
        }

        _logger?.LogInformation("Created PayOS payout: ProviderPayoutId={PayoutId} ApprovalState={ApprovalState} TransactionState={TransactionState} ResultCode={ResultCode}", payoutIdVal, approvalState, transactionState, resultCode);

        return new PayOSPayoutResult(ResultCode: resultCode, PayoutId: payoutIdVal, Message: approvalState, RequestRaw: requestJson, ResponseRaw: responseJson, TransId: transId);
    }

    public async Task<PayOSPayoutResult> QueryBankPayoutStatusAsync(string payoutId, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Querying PayOS payout status: {PayoutId}", payoutId);

        if (_client == null) throw new InvalidOperationException("PayOS client is not configured.");

        Payout? payoutResponse = null;
        try
        {
            payoutResponse = await _client.Payouts.GetAsync(payoutId);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "PayOS GetAsync failed for PayoutId={PayoutId}, will try ListAsync by ReferenceId", payoutId);
            var page = await _client.Payouts.ListAsync(new GetPayoutListParam { ReferenceId = payoutId });
            payoutResponse = page.Data.Count > 0 ? page.Data[0] : null;
        }

        if (payoutResponse == null)
        {
            _logger?.LogWarning("PayOS payout not found: {PayoutId}", payoutId);
            return new PayOSPayoutResult(ResultCode: 99, PayoutId: payoutId, Message: "not_found", RequestRaw: string.Empty, ResponseRaw: string.Empty);
        }

        var responseJson = JsonSerializer.Serialize(payoutResponse);
        _logger?.LogDebug("PayOS query response: {ResponseJson}", responseJson);

        var approvalState = payoutResponse.ApprovalState.ToString();
        var transactionState = GetPrimaryTransactionState(payoutResponse.Transactions);
        var resultCode = MapProviderStatusToResultCode(approvalState, transactionState);

        var payoutIdVal2 = !string.IsNullOrEmpty(payoutResponse.Id) ? payoutResponse.Id : payoutResponse.ReferenceId;
        string? transId2 = null;
        if (payoutResponse.Transactions != null && payoutResponse.Transactions.Count > 0)
        {
            transId2 = payoutResponse.Transactions[0].Id;
        }

        _logger?.LogInformation("PayOS payout status: ProviderPayoutId={PayoutId} ApprovalState={ApprovalState} TransactionState={TransactionState} ResultCode={ResultCode}", payoutIdVal2, approvalState, transactionState, resultCode);

        return new PayOSPayoutResult(ResultCode: resultCode, PayoutId: payoutIdVal2, Message: approvalState, RequestRaw: string.Empty, ResponseRaw: responseJson, TransId: transId2);
    }

    private static string? GetPrimaryTransactionState(object? transactions)
    {
        if (transactions is not IEnumerable enumerable)
        {
            return null;
        }

        foreach (var item in enumerable)
        {
            if (item == null)
            {
                continue;
            }

            var stateProperty = item.GetType().GetProperty("State");
            var stateValue = stateProperty?.GetValue(item)?.ToString();
            if (!string.IsNullOrWhiteSpace(stateValue))
            {
                return stateValue;
            }

            break;
        }

        return null;
    }

    private static int MapProviderStatusToResultCode(string? approvalState, string? transactionState)
    {
        if (IsSuccessState(approvalState) || IsSuccessState(transactionState))
        {
            return 0;
        }

        if (IsPendingState(approvalState) || IsPendingState(transactionState))
        {
            return 7000;
        }

        if (IsFailedState(approvalState) || IsFailedState(transactionState))
        {
            return 99;
        }

        return 99;
    }

    private static bool IsSuccessState(string? state)
        => !string.IsNullOrWhiteSpace(state) && SuccessStates.Contains(state.Trim());

    private static bool IsPendingState(string? state)
        => !string.IsNullOrWhiteSpace(state) && PendingStates.Contains(state.Trim());

    private static bool IsFailedState(string? state)
        => !string.IsNullOrWhiteSpace(state) && FailedStates.Contains(state.Trim());

    private static Exception MapApiExceptionToDomainException(ProviderErrorDetails details, Exception inner)
    {
        if (IsInsufficientBalanceError(details))
        {
            return new InvalidOperationException("PayOS payout failed because the provider account balance is insufficient. Please top up the PayOS wallet and retry.", inner);
        }

        return details.StatusCode switch
        {
            400 => new ArgumentException("Payout request was rejected by provider. Please verify bank code, destination account, and amount.", inner),
            401 => new InvalidOperationException("Payout provider credentials are invalid or expired.", inner),
            403 => new InvalidOperationException("Payout provider rejected request due to insufficient permission.", inner),
            404 => new InvalidOperationException("Payout provider endpoint or resource was not found.", inner),
            429 => new InvalidOperationException("Payout provider is rate limiting requests. Please retry shortly.", inner),
            _ => new InvalidOperationException("Payout provider failed to process the payout request.", inner)
        };
    }

    private static bool IsInsufficientBalanceError(ProviderErrorDetails details)
    {
        if (string.Equals(details.ProviderCode, "624", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var providerMessage = details.ProviderMessage;
        if (string.IsNullOrWhiteSpace(providerMessage))
        {
            return false;
        }

        return providerMessage.Contains("insufficient balance", StringComparison.OrdinalIgnoreCase)
            || providerMessage.Contains("khong du", StringComparison.OrdinalIgnoreCase)
            || providerMessage.Contains("so du", StringComparison.OrdinalIgnoreCase)
            || providerMessage.Contains("balance is insufficient", StringComparison.OrdinalIgnoreCase);
    }

    private static ProviderErrorDetails ExtractProviderErrorDetails(Exception ex)
    {
        var statusCode = TryGetIntProperty(ex, "StatusCode", "HttpStatusCode");
        var providerCode = TryGetStringProperty(ex, "Code", "ErrorCode", "Type");
        var providerMessage = TryGetStringProperty(ex, "Description", "Desc");
        var correlationId = TryGetStringProperty(ex, "CorrelationId", "RequestId", "TraceId");

        if (string.IsNullOrWhiteSpace(providerMessage))
        {
            providerMessage = ex.Message;
        }

        var responseDataObj = TryGetPropertyValue(ex, "ResponseData");
        if (responseDataObj is IDictionary responseData)
        {
            statusCode ??= TryGetIntFromDictionary(responseData, "statusCode", "status", "httpStatus");
            providerCode ??= TryGetStringFromDictionary(responseData, "code", "errorCode", "type");
            correlationId ??= TryGetStringFromDictionary(responseData, "correlationId", "requestId", "traceId", "x-request-id");

            var messageFromData = TryGetStringFromDictionary(responseData, "message", "desc", "description", "error");
            if (!string.IsNullOrWhiteSpace(messageFromData))
            {
                providerMessage = messageFromData;
            }
        }

        return new ProviderErrorDetails(statusCode, providerCode, providerMessage, correlationId);
    }

    private static object? TryGetPropertyValue(object source, string propertyName)
    {
        var property = source.GetType().GetProperty(propertyName);
        return property?.GetValue(source);
    }

    private static string? TryGetStringProperty(object source, params string[] names)
    {
        foreach (var name in names)
        {
            var value = TryGetPropertyValue(source, name);
            if (value == null)
            {
                continue;
            }

            var text = value.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return null;
    }

    private static int? TryGetIntProperty(object source, params string[] names)
    {
        foreach (var name in names)
        {
            var value = TryGetPropertyValue(source, name);
            if (value == null)
            {
                continue;
            }

            if (value is int i)
            {
                return i;
            }

            if (value is System.Net.HttpStatusCode status)
            {
                return (int)status;
            }

            if (int.TryParse(value.ToString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static string? TryGetStringFromDictionary(IDictionary dictionary, params string[] keys)
    {
        foreach (var key in keys)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key?.ToString()?.Equals(key, StringComparison.OrdinalIgnoreCase) == true)
                {
                    var text = entry.Value?.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }
        }

        return null;
    }

    private static int? TryGetIntFromDictionary(IDictionary dictionary, params string[] keys)
    {
        var text = TryGetStringFromDictionary(dictionary, keys);
        return int.TryParse(text, out var parsed) ? parsed : null;
    }

    private sealed record ProviderErrorDetails(
    int? StatusCode,
    string? ProviderCode,
    string? ProviderMessage,
    string? CorrelationId);
}
