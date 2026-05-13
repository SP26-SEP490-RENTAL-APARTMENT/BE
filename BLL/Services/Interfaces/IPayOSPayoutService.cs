using System.Threading;
using System.Threading.Tasks;

namespace BLL.Services.Interfaces;

public record PayOSPayoutResult(int ResultCode, string? PayoutId, string? Message, string? RequestRaw, string? ResponseRaw, string? TransId = null);

public interface IPayOSPayoutService
{
    Task<PayOSPayoutResult> CreateBankPayoutAsync(string receiverName, string accountOrCard, string bankCode, long amount, string reference, CancellationToken cancellationToken = default);

    Task<PayOSPayoutResult> QueryBankPayoutStatusAsync(string payoutId, CancellationToken cancellationToken = default);
}
