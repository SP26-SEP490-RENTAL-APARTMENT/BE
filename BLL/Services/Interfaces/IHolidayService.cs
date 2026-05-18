using System;
using System.Threading.Tasks;

namespace BLL.Services.Interfaces;

public interface IHolidayService
{
    Task<bool> IsHolidayAsync(DateOnly date, string? locationScope = null);
}
