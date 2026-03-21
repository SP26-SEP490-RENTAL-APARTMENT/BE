using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class MomoTransaction
{
    public int Id { get; set; }

    public string RequestId { get; set; } = null!;

    public string PartnerCode { get; set; } = null!;

    public long Amount { get; set; }

    public string Type { get; set; } = null!;

    public string RequestBody { get; set; } = null!;

    public string ResponseBody { get; set; } = null!;

    public string Status { get; set; } = null!;

    public int? ResultCode { get; set; }

    public string Message { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? PaymentId { get; set; }

    public virtual Payment? Payment { get; set; }
}
