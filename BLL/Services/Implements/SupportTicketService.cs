using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;
using NotificationType = Common.Enums.Notification;

namespace BLL.Services.Implements
{
    public class SupportTicketService : BaseService<SupportTicket>, ISupportTicketService
    {
        private readonly ISupportTicketRepository _supportTicketRepository;
        private readonly IUserRepository _userRepository;
        private readonly IRepository<SupportTicketAssignment> _assignmentRepository;
        private readonly IRepository<Notification> _notificationRepository;
        private readonly IRepository<SupportTicketAttachment> _attachmentRepository;
        private readonly IImageService _imageService;

        public SupportTicketService(
            ISupportTicketRepository repository,
            IUserRepository userRepository,
            IRepository<SupportTicketAssignment> assignmentRepository,
            IRepository<Notification> notificationRepository,
            IRepository<SupportTicketAttachment> attachmentRepository,
            IImageService imageService) : base(repository)
        {
            _supportTicketRepository = repository;
            _userRepository = userRepository;
            _assignmentRepository = assignmentRepository;
            _notificationRepository = notificationRepository;
            _attachmentRepository = attachmentRepository;
            _imageService = imageService;
        }

        private async Task CreateSupportNotificationAsync(
            Guid userId,
            string type,
            string title,
            string message,
            Guid ticketId,
            bool saveChanges = true,
            string? titleVi = null,
            string? messageVi = null)
        {
            await _notificationRepository.AddAsync(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = userId,
                Type = type,
                Title = title,
                TitleVi = titleVi,
                Message = message,
                MessageVi = messageVi,
                ReferenceId = ticketId,
                ReferenceType = "support_ticket",
                IsRead = false,
                CreatedAt = Common.Utils.VietnamTime.Now
            });

            if (saveChanges)
            {
                await _notificationRepository.SaveChangesAsync();
            }
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
            ticket.CreatedAt ??= Common.Utils.VietnamTime.Now;
            ticket.UpdatedAt = Common.Utils.VietnamTime.Now;

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
                    AssignedAt = Common.Utils.VietnamTime.Now,
                    RoleInTicket = "primary"
                };

                await _assignmentRepository.AddAsync(assignment);
                await _assignmentRepository.SaveChangesAsync();

                foreach (var staff in staffUsers)
                {
                    await CreateSupportNotificationAsync(
                        staff.UserId,
                        NotificationType.support_ticket_created.ToString(),
                        "New support ticket reported",
                        $"Ticket '{ticket.Subject}' requires attention.",
                        ticket.TicketId,
                        saveChanges: false,
                        titleVi: "Phiếu hỗ trợ mới được báo cáo",
                        messageVi: $"Phiếu '{ticket.Subject}' cần được xử lý.");
                }
                await _notificationRepository.SaveChangesAsync();
            }

            await CreateSupportNotificationAsync(
                ticket.UserId,
                NotificationType.support_ticket_created.ToString(),
                "Support ticket received",
                $"Your ticket '{ticket.Subject}' has been received. Our staff will review it shortly.",
                ticket.TicketId,
                titleVi: "Đã nhận phiếu hỗ trợ",
                messageVi: $"Phiếu '{ticket.Subject}' của bạn đã được nhận. Nhân viên sẽ xem xét sớm.");

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
            ticket.UpdatedAt = Common.Utils.VietnamTime.Now;

            var isResolved = string.Equals(ticketDto.Status, "resolved", StringComparison.OrdinalIgnoreCase);
            if (isResolved)
            {
                ticket.ResolvedAt = ticketDto.ResolvedAt ?? Common.Utils.VietnamTime.Now;
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
            var titleVi = isResolved ? "Phiếu hỗ trợ của bạn đã được giải quyết" : "Phiếu hỗ trợ của bạn đã được cập nhật";
            var message = isResolved
                ? $"Ticket '{ticket.Subject}' has been resolved. Please verify the fix."
                : $"Ticket '{ticket.Subject}' has a status update: {ticket.Status}.";
            var messageVi = isResolved
                ? $"Phiếu '{ticket.Subject}' đã được giải quyết. Vui lòng xác nhận kết quả."
                : $"Phiếu '{ticket.Subject}' có cập nhật trạng thái: {ticket.Status}.";

            await CreateSupportNotificationAsync(
                ticket.UserId,
                type,
                title,
                message,
                ticket.TicketId,
                titleVi: titleVi,
                messageVi: messageVi);

            var assignments = await _assignmentRepository.FindAsync(a => a.TicketId == ticketId);
            foreach (var assignment in assignments)
            {
                var staffTitle = isResolved
                    ? "Assigned ticket resolved"
                    : "Assigned ticket updated";
                var staffTitleVi = isResolved
                    ? "Phiếu được phân công đã giải quyết"
                    : "Phiếu được phân công đã cập nhật";

                var staffMessage = isResolved
                    ? $"Ticket '{ticket.Subject}' has been resolved."
                    : $"Ticket '{ticket.Subject}' status updated to {ticket.Status}.";
                var staffMessageVi = isResolved
                    ? $"Phiếu '{ticket.Subject}' đã được giải quyết."
                    : $"Trạng thái phiếu '{ticket.Subject}' đã cập nhật thành {ticket.Status}.";

                await CreateSupportNotificationAsync(
                    assignment.StaffId,
                    type,
                    staffTitle,
                    staffMessage,
                    ticket.TicketId,
                    saveChanges: false,
                    titleVi: staffTitleVi,
                    messageVi: staffMessageVi);
            }

            if (assignments.Any())
            {
                await _notificationRepository.SaveChangesAsync();
            }

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
            original.UpdatedAt = Common.Utils.VietnamTime.Now;
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
                CreatedAt = Common.Utils.VietnamTime.Now,
                UpdatedAt = Common.Utils.VietnamTime.Now
            };

            return await CreateTicketAsync(followUp);
        }

        public async Task<SupportTicket> ResolveTicketByStaffAsync(
            Guid ticketId,
            string resolutionNotes,
            Guid staffActorUserId)
        {
            var ticket = await _supportTicketRepository.GetByIdAsync(ticketId);
            if (ticket == null)
            {
                throw new ArgumentException("Support ticket not found.");
            }

            // 1. Basic Validation
            if (string.IsNullOrWhiteSpace(resolutionNotes))
            {
                throw new ArgumentException("Resolution notes are required to resolve the ticket.");
            }

            // Optimization: Check if the ticket is already resolved
            if (string.Equals(ticket.Status, "resolved", StringComparison.OrdinalIgnoreCase))
            {
                // Optionally throw or just return the ticket if already resolved.
                return ticket;
            }

            // 2. Update Ticket Status and Details

            // Set the status to resolved
            ticket.Status = "resolved";

            // Set required resolution metadata
            ticket.ResolutionNotes = resolutionNotes;
            ticket.ResolvedAt = Common.Utils.VietnamTime.Now;
            ticket.ResolvedBy = staffActorUserId;
            ticket.UpdatedAt = Common.Utils.VietnamTime.Now;

            _supportTicketRepository.Update(ticket);
            await _supportTicketRepository.SaveChangesAsync();

            // 3. Handle Notifications

            var type = "support_ticket_resolved";
            var title = "Your support ticket was resolved";
            var message = $"Ticket '{ticket.Subject}' has been resolved by staff. Please review the resolution notes.";

            // A. Notify the original requester
            await CreateSupportNotificationAsync(
                ticket.UserId,
                type,
                title,
                message,
                ticket.TicketId,
                titleVi: "Phiếu hỗ trợ của bạn đã được giải quyết",
                messageVi: $"Phiếu '{ticket.Subject}' đã được nhân viên giải quyết. Vui lòng xem ghi chú giải quyết.");

            // B. Notify all staff assigned to the ticket
            var assignments = await _assignmentRepository.FindAsync(a => a.TicketId == ticketId);
            foreach (var assignment in assignments)
            {
                var staffTitle = "Assigned ticket resolved";
                var staffMessage = $"Ticket '{ticket.Subject}' has been resolved by {staffActorUserId}.";

                await CreateSupportNotificationAsync(
                    assignment.StaffId,
                    type,
                    staffTitle,
                    staffMessage,
                    ticket.TicketId,
                    saveChanges: false,
                    titleVi: "Phiếu được phân công đã giải quyết",
                    messageVi: $"Phiếu '{ticket.Subject}' đã được giải quyết bởi {staffActorUserId}.");
            }

            // 4. Save all pending notifications
            if (assignments.Any())
            {
                await _notificationRepository.SaveChangesAsync();
            }

            return ticket;
        }

        public async Task<SupportTicket> UpdateTicketByCreatorStatusAsync(
            Guid ticketId,
            Guid requesterUserId,
            UserUpdateStatusRequestDto updateDto)
        {
            var ticket = await _supportTicketRepository.GetByIdAsync(ticketId);
            if (ticket == null)
            {
                throw new ArgumentException("Support ticket not found.");
            }

            // 1. Authorization Check: Must be the original creator
            if (ticket.UserId != requesterUserId)
            {
                throw new UnauthorizedAccessException("You can only update the status of tickets you created.");
            }

            // 2. Business Logic Check: Cannot transition if already closed
            var currentStatus = string.Equals(ticket.Status, "closed", StringComparison.OrdinalIgnoreCase) ? "closed" : ticket.Status;
            if (string.Equals(currentStatus, "closed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("This ticket is already closed and cannot be modified.");
            }

            // 3. Status Validation
            var newStatus = updateDto.NewStatus.ToLower();
            if (newStatus != "closed" && newStatus != "escalated")
            {
                throw new ArgumentException("Invalid target status. Only 'closed' or 'escalated' changes are allowed from the creator.");
            }

            // 4. Update Logic
            var oldStatus = ticket.Status;

            // Update the status and notes
            ticket.Status = newStatus;
            ticket.UpdatedAt = Common.Utils.VietnamTime.Now;

            if (string.IsNullOrEmpty(updateDto.StatusChangeNotes))
            {
                // If changing to closed, we might want to force resolution notes if none provided
                ticket.ResolutionNotes = updateDto.StatusChangeNotes ?? $"Creator initiated status change from {oldStatus} to {newStatus}.";
            }
            else
            {
                ticket.ResolutionNotes = updateDto.StatusChangeNotes;
            }

            _supportTicketRepository.Update(ticket);
            await _supportTicketRepository.SaveChangesAsync();


            // 5. Notification Handling
            if (string.Equals(newStatus, "closed", StringComparison.OrdinalIgnoreCase))
            {
                // Optionally set resolved dates if closing
                ticket.ResolvedAt = Common.Utils.VietnamTime.Now;
                ticket.ResolvedBy = requesterUserId;
            }

            // Determine notification type and title
            var type = string.Equals(newStatus, "closed", StringComparison.OrdinalIgnoreCase)
                ? "support_ticket_closed_by_user"
                : "support_ticket_re_escalated_by_user";

            var title = string.Equals(newStatus, "closed", StringComparison.OrdinalIgnoreCase)
                ? "Support Ticket Closed"
                : "Support Ticket Re-escalated";
            var titleVi = string.Equals(newStatus, "closed", StringComparison.OrdinalIgnoreCase)
                ? "Phiếu hỗ trợ đã đóng"
                : "Phiếu hỗ trợ được leo thang";

            var message = string.Equals(newStatus, "closed", StringComparison.OrdinalIgnoreCase)
                ? "The ticket has been closed by the creator. Please check the notes."
                : $"The ticket status was changed to {newStatus}.";
            var messageVi = string.Equals(newStatus, "closed", StringComparison.OrdinalIgnoreCase)
                ? "Phiếu đã được người tạo đóng lại. Vui lòng kiểm tra ghi chú."
                : $"Trạng thái phiếu đã thay đổi thành {newStatus}.";

            // Notify the creator themselves
            await CreateSupportNotificationAsync(
                requesterUserId,
                type,
                title,
                message,
                ticket.TicketId,
                titleVi: titleVi,
                messageVi: messageVi);

            // Notify all staff assigned (to make them aware of the major status change)
            var assignments = await _assignmentRepository.FindAsync(a => a.TicketId == ticketId);
            foreach (var assignment in assignments)
            {
                var staffMessage = string.Equals(newStatus, "closed", StringComparison.OrdinalIgnoreCase)
                    ? $"The ticket '{ticket.Subject}' has been manually closed by the creator ({requesterUserId})."
                    : $"The ticket '{ticket.Subject}' has been re-escalated by the creator ({requesterUserId}).";
                var staffMessageVi = string.Equals(newStatus, "closed", StringComparison.OrdinalIgnoreCase)
                    ? $"Phiếu '{ticket.Subject}' đã được người tạo ({requesterUserId}) đóng thủ công."
                    : $"Phiếu '{ticket.Subject}' đã được người tạo ({requesterUserId}) leo thang.";

                await CreateSupportNotificationAsync(
                    assignment.StaffId,
                    type,
                    "Creator Status Change",
                    staffMessage,
                    ticket.TicketId,
                    saveChanges: false,
                    titleVi: "Thay đổi trạng thái bởi người tạo",
                    messageVi: staffMessageVi);
            }

            if (assignments.Any())
            {
                await _notificationRepository.SaveChangesAsync();
            }

            return ticket;
        }

        public async Task<IEnumerable<SupportTicketAttachment>> UploadTicketAttachmentsAsync(
            Guid ticketId,
            UploadSupportTicketAttachmentDto dto,
            Guid uploadedByUserId)
        {
            // 1. Validate ticket exists
            var ticket = await _supportTicketRepository.GetByIdAsync(ticketId);
            if (ticket == null)
            {
                throw new ArgumentException("Support ticket not found.");
            }

            // 2. Validate files
            if (dto.Files == null || dto.Files.Count == 0)
            {
                throw new ArgumentException("At least one file is required for upload.");
            }

            var uploadedAttachments = new List<SupportTicketAttachment>();

            // 3. Upload each file
            foreach (var file in dto.Files)
            {
                if (file.Length == 0)
                {
                    continue; // Skip empty files
                }

                try
                {
                    // Upload image to Cloudinary
                    var fileUrl = await _imageService.UploadImageAsync(file);

                    if (string.IsNullOrEmpty(fileUrl))
                        throw new InvalidOperationException($"Upload returned no URL for file '{file.FileName}'.");

                    // Create attachment record
                    var attachment = new SupportTicketAttachment
                    {
                        AttachmentId = Guid.NewGuid(),
                        TicketId = ticketId,
                        FileUrl = fileUrl,
                        MimeType = file.ContentType,
                        FileSize = file.Length,
                        UploadedAt = Common.Utils.VietnamTime.Now,
                        UploadedBy = uploadedByUserId,
                        Caption = dto.Caption,
                        IsEvidence = dto.IsEvidence
                    };

                    await _attachmentRepository.AddAsync(attachment);
                    uploadedAttachments.Add(attachment);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to upload file '{file.FileName}': {ex.Message}");
                }
            }

            // 4. Save all attachments
            if (uploadedAttachments.Count > 0)
            {
                await _attachmentRepository.SaveChangesAsync();

                // Notify staff that evidence has been uploaded
                var assignments = await _assignmentRepository.FindAsync(a => a.TicketId == ticketId);
                foreach (var assignment in assignments)
                {
                    await CreateSupportNotificationAsync(
                        assignment.StaffId,
                        "support_ticket_evidence_uploaded",
                        "Evidence uploaded to ticket",
                        $"New evidence ({uploadedAttachments.Count} file(s)) has been uploaded to ticket '{ticket.Subject}'.",
                        ticketId,
                        saveChanges: false,
                        titleVi: "Bằng chứng đã tải lên phiếu",
                        messageVi: $"Bằng chứng mới ({uploadedAttachments.Count} tệp) đã được tải lên phiếu '{ticket.Subject}'.");
                }

                if (assignments.Any())
                {
                    await _notificationRepository.SaveChangesAsync();
                }
            }

            return uploadedAttachments;
        }
    }
}