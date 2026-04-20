using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace BLL.Services.Implements
{
    public sealed class MomoTransactionService(IRepository<MomoTransaction> repository) : BaseService<MomoTransaction>(repository), IMomoTransactionService
    {
        public async Task<MomoTransaction?> FindByRequestIdAsync(string requestId)
        {
            var items = await _repository.FindAsync(x => x.RequestId == requestId);
            return items.FirstOrDefault();
        }

        public async Task<MomoTransaction?> FindByRequestBodyContainsAsync(string content)
        {
            var items = await _repository.FindAsync(x => x.RequestBody.Contains(content));
            return items.FirstOrDefault();
        }

        public async Task<IReadOnlyList<MomoTransaction>> GetPendingIpnQueueItemsAsync(int take, DateTime retryReadyAtOrBefore)
        {
            var items = await _repository.FindAsync(x =>
                x.Type == "ipn_queue"
                && (x.Status == "queued" || (x.Status == "retry_wait" && x.UpdatedAt <= retryReadyAtOrBefore)));

            return items
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .Take(take)
                .ToList();
        }

        public async Task<IReadOnlyList<MomoTransaction>> GetPendingWalletPaymentRequestsAsync(int take, DateTime createdBefore)
        {
            var pendingTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "create_wallet_payment",
                "create_wallet_payment_subscription"
            };

            var items = await _repository.FindAsync(x =>
                pendingTypes.Contains(x.Type)
                && x.Status == "pending"
                && x.CreatedAt <= createdBefore);

            return items
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .Take(take)
                .ToList();
        }
    }
}
