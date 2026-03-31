using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class MomoCreatePaymentRequest
    {
        public long Amount { get; set; }
        public string OrderInfo { get; set; } = string.Empty;
        public string? ExtraData { get; set; }
        public string? PaymentType { get; set; }
        public string? PaymentPurpose { get; set; }
    }

    public class MomoCreatePaymentResponse
    {
        public int ResultCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? PayUrl { get; set; }
        public string? Deeplink { get; set; }
        public string? QrCodeUrl { get; set; }
        public string? ResponseRaw { get; set; }

        // Additional metadata used for persistence
        public string OrderId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public long Amount { get; set; }
        public string? RequestRaw { get; set; }
    }

    public class MomoVerifyWalletRequest
    {
        public string WalletId { get; set; } = string.Empty;
        public string WalletName { get; set; } = string.Empty;
        public string? PersonalId { get; set; }
        public string Lang { get; set; } = "vi";
    }

    public class MomoDisbursementRequest
    {
        public long Amount { get; set; }
        public string OrderInfo { get; set; } = string.Empty;
        public string RequestType { get; set; } = "disburseToWallet";
        public string ReceiverAccount { get; set; } = string.Empty;
        public string ReceiverName { get; set; } = string.Empty;
        public string? PersonalId { get; set; }
        public string? BankCode { get; set; }
        public string? ExtraData { get; set; }
        public string Lang { get; set; } = "vi";
    }

    public class MomoDisbursementResponse
    {
        public int ResultCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public long Amount { get; set; }
        public string? TransId { get; set; }
        public long? Balance { get; set; }
        public string? RequestRaw { get; set; }
        public string? ResponseRaw { get; set; }
    }

    public class MomoQueryDisbursementRequest
    {
        public string OrderId { get; set; } = string.Empty;
        public string? RequestId { get; set; }
        public string Lang { get; set; } = "vi";
    }

    public class MomoQueryDisbursementResponse
    {
        public int ResultCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public string? TransId { get; set; }
        public string? ResponseRaw { get; set; }
    }

    public class MomoDisbursementIpnDto
    {
        public string PartnerCode { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public long Amount { get; set; }
        public string OrderInfo { get; set; } = string.Empty;
        public string? PartnerUserId { get; set; }
        public string OrderType { get; set; } = string.Empty;
        public string TransId { get; set; } = string.Empty;
        public int ResultCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public long ResponseTime { get; set; }
        public string? ExtraData { get; set; }
        public string Signature { get; set; } = string.Empty;
    }

}
