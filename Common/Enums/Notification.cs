using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Enums
{
    public enum Notification
    {
        booking_created, 
        booking_confirmed, 
        booking_cancelled, 
        booking_upcoming, 
        payment_success, 
        payment_failed, 
        identity_verified, 
        identity_rejected, 
        listing_approved, 
        listing_rejected, 
        inspection_scheduled, 
        inspection_completed, 
        support_ticket_created, 
        support_ticket_update, 
        support_ticket_resolved, 
        review_reminder, 
        new_message, 
        system_announcement, 
        check_in_recorded,
        check_out_recorded,
        check_time_confirmed,
        check_time_disputed,
        check_time_dispute_resolved,
        other
    }
}
