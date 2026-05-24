using System;

namespace Common.DTOs;

public class LandlordDashboardSummaryDto
{
    public DateTime GeneratedAt { get; set; }
    public int TotalProperties { get; set; }
    public int TotalBookings { get; set; }
    public decimal TotalRevenue { get; set; }
}