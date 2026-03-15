using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Enums
{
    public enum Status
    {
        active, pending_payment, expired, cancelled, trial
    }

    public enum RenewalType
    {
        monthly, annual, none
    }
}
