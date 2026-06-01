using System;

namespace Common.DTOs;

public class MediaAssetDto
{
    public Guid? MediaId { get; set; }

    public string Url { get; set; } = null!;

    public string? MediaType { get; set; }

    public bool? IsPrimary { get; set; }
}