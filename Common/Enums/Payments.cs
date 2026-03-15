using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Enums
{
    public enum PaymentTypes
    {
        deposit, balance, addon, refund
    }
    public enum PaymentPurposes
    {
        booking_deposit, 
        booking_balance, 
        booking_addon_or_package, 
        subscription_monthly, 
        subscription_annual, 
        subscription_trial, 
        subscription_renewal, 
        refund_booking, 
        refund_subscription, 
        other
    }
    public enum PaymentRelatedEntityType
    {
        booking, host_subscription, other
    }
    public enum PaymentStatus
    {
        pending, success, failed, refunded
    }


}
