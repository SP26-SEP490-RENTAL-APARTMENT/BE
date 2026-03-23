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
    }
}
