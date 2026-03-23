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

}
