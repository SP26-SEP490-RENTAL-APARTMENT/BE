using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Enums
{
    public enum SupportCategory
    {
        booking_issue, 
        payment_problem, 
        listing_problem, 
        account_verification, 
        cancellation, 
        dispute, 
        property_quality, 
        other
    }

    public enum  SupportPriority
    {
        low, medium, high, urgent
    }
    public enum  SupportStatus
    {
        open, in_progress, resolved, closed, escalated
    }
}
