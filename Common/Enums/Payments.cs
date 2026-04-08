using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Enums
{
    public enum BookingPaymentMode
    {
        partial,
        full
    }

    public enum PaymentTypes
    {
        deposit, balance, addon, refund, upfront
    }
    public enum PaymentPurposes
    {
        booking_deposit, 
        booking_balance, 
        booking_full_payment,
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
