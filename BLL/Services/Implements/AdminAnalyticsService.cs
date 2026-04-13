using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public class AdminAnalyticsService : IAdminAnalyticsService
{
    private readonly IRepository<Booking> _bookingRepository;
    private readonly IRepository<SupportTicket> _supportTicketRepository;

    public AdminAnalyticsService(
        IRepository<Booking> bookingRepository,
        IRepository<SupportTicket> supportTicketRepository)
    {
        _bookingRepository = bookingRepository;
        _supportTicketRepository = supportTicketRepository;
    }

    public async Task<AdminAnalyticsSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var now = Common.Utils.VietnamTime.Now;
        var windowStart = now.AddHours(-24);

        var bookings = (await _bookingRepository.FindAsync(_ => true)).ToList();
        var openTickets = await _supportTicketRepository.FindAsync(t =>
            t.Status != null &&
            (t.Status.ToLower() == "open" || t.Status.ToLower() == "in_progress" || t.Status.ToLower() == "escalated"));

        var paidStatuses = new[] { "paid", "completed" };

        var paidAndCompleted = bookings.Where(b =>
            b.Status != null && paidStatuses.Contains(b.Status, StringComparer.OrdinalIgnoreCase)).ToList();

        return new AdminAnalyticsSnapshotDto
        {
            GeneratedAt = now,
            TotalBookings = bookings.Count,
            PaidBookings = bookings.Count(b => string.Equals(b.Status, "paid", StringComparison.OrdinalIgnoreCase)),
            CompletedBookings = bookings.Count(b => string.Equals(b.Status, "completed", StringComparison.OrdinalIgnoreCase)),
            BookingsLast24Hours = bookings.Count(b => b.CreatedAt.HasValue && b.CreatedAt.Value >= windowStart),
            RevenueFromPaidAndCompleted = paidAndCompleted.Sum(b => b.TotalPrice),
            OpenSupportTickets = openTickets.Count()
        };
    }
}
