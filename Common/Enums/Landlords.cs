using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Enums
{
    public enum IdentityVerificationStatus
    {
        not_started, pending, verified, rejected
    }
    public enum SubscriptionStatus
    {
        none, active, expired, pending
    }
}
