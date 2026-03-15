using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Enums
{
    public enum ApartmentPriceCalendar
    {
        @base, 
        weekend, 
        holiday, 
        peak_season, 
        low_season, 
        special_event, 
        manual_override
    }
}
