using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements
{
    public class SupportTicketService : BaseService<SupportTicket>, ISupportTicketService
    {
        private readonly ISupportTicketRepository _supportTicketRepository;
        private readonly IUserRepository _userRepository;
        private readonly IRepository<SupportTicketAssignment> _assignmentRepository;
        private readonly IRepository<Notification> _notificationRepository;

        public SupportTicketService(
            ISupportTicketRepository repository,
            IUserRepository userRepository,
            IRepository<SupportTicketAssignment> assignmentRepository,
            IRepository<Notification> notificationRepository) : base(repository)
        {
            _supportTicketRepository = repository;
            _userRepository = userRepository;
            _assignmentRepository = assignmentRepository;
            _notificationRepository = notificationRepository;
        }

        public override async Task<(IEnumerable<SupportTicket> Items, int TotalCount)> GetAllAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null,
            IEnumerable<string>? allowedColumns = null)
        {
            var effectiveAllowedColumns = new[]
            {
                "TicketId",
                "UserId",
                "Subject",
                "Description",
                "Category",
                "Priority",
                "Status",
                "CreatedAt",
                "ResolvedAt",
                "ResolvedBy"
            };

            return await base.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters, effectiveAllowedColumns);
        }

        public async Task<SupportTicket> CreateTicketAsync(SupportTicket ticket)
        {
            ticket.Status ??= "open";
            ticket.CreatedAt ??= DateTime.UtcNow;
            ticket.UpdatedAt = DateTime.UtcNow;

            await _supportTicketRepository.AddAsync(ticket);
            await _supportTicketRepository.SaveChangesAsync();

            var staffUsers = (await _userRepository.FindAsync(u => u.Role.ToLower() == "staff")).ToList();
            if (staffUsers.Count > 0)
            {
                var primaryStaff = staffUsers
                    .OrderBy(u => u.CreatedAt ?? DateTime.MaxValue)
                    .ThenBy(u => u.UserId)
                    .First();

                var assignment = new SupportTicketAssignment
                {
                    AssignmentId = Guid.NewGuid(),
                    TicketId = ticket.TicketId,
                    StaffId = primaryStaff.UserId,
                    AssignedAt = DateTime.UtcNow,
                    RoleInTicket = "primary"
                };

                await _assignmentRepository.AddAsync(assignment);
                await _assignmentRepository.SaveChangesAsync();

                foreach (var staff in staffUsers)
                {
                    await _notificationRepository.AddAsync(new Notification
                    {
                        NotificationId = Guid.NewGuid(),
                        UserId = staff.UserId,
                        Type = "support_ticket_created",
                        Title = "New support ticket reported",
                        Message = $"Ticket '{ticket.Subject}' requires attention.",
                        ReferenceId = ticket.TicketId,
                        ReferenceType = "support_ticket",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                await _notificationRepository.SaveChangesAsync();
            }

            return ticket;
        }

        public async Task<SupportTicket> UpdateTicketByStaffAsync(Guid ticketId, UpdateSupportTicketDto ticketDto, Guid actorUserId)
        {
            var ticket = await _supportTicketRepository.GetByIdAsync(ticketId);
            if (ticket == null)
            {
                throw new ArgumentException("Support ticket not found.");
            }

            ticket.Subject = ticketDto.Subject;
            ticket.Description = ticketDto.Description;
            ticket.Category = ticketDto.Category;
            ticket.Priority = ticketDto.Priority;
            ticket.Status = ticketDto.Status;
            ticket.ResolutionNotes = ticketDto.ResolutionNotes;
            ticket.UpdatedAt = DateTime.UtcNow;

            var isResolved = string.Equals(ticketDto.Status, "resolved", StringComparison.OrdinalIgnoreCase);
            if (isResolved)
            {
                ticket.ResolvedAt = ticketDto.ResolvedAt ?? DateTime.UtcNow;
                ticket.ResolvedBy = ticketDto.ResolvedBy ?? actorUserId;
            }
            else
            {
                ticket.ResolvedAt = ticketDto.ResolvedAt;
                ticket.ResolvedBy = ticketDto.ResolvedBy;
            }

            _supportTicketRepository.Update(ticket);
            await _supportTicketRepository.SaveChangesAsync();

            var type = isResolved ? "support_ticket_resolved" : "support_ticket_update";
            var title = isResolved ? "Your support ticket was resolved" : "Your support ticket was updated";
            var message = isResolved
                ? $"Ticket '{ticket.Subject}' has been resolved. Please verify the fix."
                : $"Ticket '{ticket.Subject}' has a status update: {ticket.Status}.";

            await _notificationRepository.AddAsync(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = ticket.UserId,
                Type = type,
                Title = title,
                Message = message,
                ReferenceId = ticket.TicketId,
                ReferenceType = "support_ticket",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
            await _notificationRepository.SaveChangesAsync();

            return ticket;
        }

        public async Task<SupportTicket> CreateFollowUpTicketAsync(Guid originalTicketId, Guid requesterUserId, string details)
        {
            var original = await _supportTicketRepository.GetByIdAsync(originalTicketId);
            if (original == null)
            {
                throw new ArgumentException("Original support ticket not found.");
            }

            if (original.UserId != requesterUserId)
            {
                throw new InvalidOperationException("You can only report persistence for your own ticket.");
            }

            if (!string.Equals(original.Status, "resolved", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(original.Status, "closed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("You can report persistence only after ticket is resolved or closed.");
            }

            original.Status = "escalated";
            original.UpdatedAt = DateTime.UtcNow;
            _supportTicketRepository.Update(original);
            await _supportTicketRepository.SaveChangesAsync();

            var followUpSubject = $"Follow-up: {original.Subject}";
            if (followUpSubject.Length > 200)
            {
                followUpSubject = followUpSubject[..200];
            }

            var followUp = new SupportTicket
            {
                UserId = requesterUserId,
                Subject = followUpSubject,
                Description = $"Follow-up for ticket {original.TicketId}: {details}",
                Category = original.Category,
                Priority = original.Priority ?? "medium",
                Status = "open",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            return await CreateTicketAsync(followUp);
        }
    }
}