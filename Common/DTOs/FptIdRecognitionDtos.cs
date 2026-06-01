using System.Collections.Generic;

namespace Common.DTOs;

public class FptIdRecognitionResult
{
    public bool Success { get; set; }

    public int ErrorCode { get; set; }

    public string ErrorMessage { get; set; } = string.Empty;

    public string? CardType { get; set; }

    public string? CardTypeDetail { get; set; }

    public string? IdNumber { get; set; }

    public string? FullName { get; set; }

    public string? DateOfBirth { get; set; }

    public string? Home { get; set; }

    public string? Address { get; set; }

    public string? Nationality { get; set; }

    public string? IssueDate { get; set; }

    public string? PassportNumber { get; set; }

    public string? PlaceOfBirth { get; set; }

    public string? Sex { get; set; }

    public string? ExpiryDate { get; set; }

    public double OverallConfidence { get; set; }

    public Dictionary<string, string> ExtractedFields { get; set; } = new();

    public Dictionary<string, double> FieldConfidences { get; set; } = new();
}
