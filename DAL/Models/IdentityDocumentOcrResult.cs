using System;

namespace DAL.Models;

public partial class IdentityDocumentOcrResult
{
    public Guid OcrResultId { get; set; }

    public Guid DocumentId { get; set; }

    public string Provider { get; set; } = null!;

    public int? ProviderErrorCode { get; set; }

    public string? ProviderErrorMessage { get; set; }

    public string? CardType { get; set; }

    public string? CardTypeDetail { get; set; }

    public string? IdNumber { get; set; }

    public string? FullName { get; set; }

    public string? DateOfBirthRaw { get; set; }

    public string? IssueDateRaw { get; set; }

    public decimal? OverallConfidence { get; set; }

    public string? ExtractedFieldsJson { get; set; }

    public string? FieldConfidencesJson { get; set; }

    public bool? AutoApproved { get; set; }

    public bool? MatchPassed { get; set; }

    public string? MatchFailureReason { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public virtual UserIdentityDocument Document { get; set; } = null!;
}
