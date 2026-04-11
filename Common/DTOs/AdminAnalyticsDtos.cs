using System;

namespace Common.DTOs;

public class AdminAnalyticsSnapshotDto
{
    public DateTime GeneratedAt { get; set; }
    public int TotalBookings { get; set; }
    public int PaidBookings { get; set; }
    public int CompletedBookings { get; set; }
    public int BookingsLast24Hours { get; set; }
    public decimal RevenueFromPaidAndCompleted { get; set; }
    public int OpenSupportTickets { get; set; }
}
