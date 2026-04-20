using DAL.Models;

namespace BLL.Services.Interfaces
{
    public interface IMomoTransactionService : IBaseService<MomoTransaction>
    {
        Task<MomoTransaction?> FindByRequestIdAsync(string requestId);
        Task<MomoTransaction?> FindByRequestBodyContainsAsync(string content);
        Task<IReadOnlyList<MomoTransaction>> GetPendingIpnQueueItemsAsync(int take, DateTime retryReadyAtOrBefore);
        Task<IReadOnlyList<MomoTransaction>> GetPendingWalletPaymentRequestsAsync(int take, DateTime createdBefore);
    }
}
