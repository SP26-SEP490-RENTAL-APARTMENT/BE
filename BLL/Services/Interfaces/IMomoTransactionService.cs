using DAL.Models;

namespace BLL.Services.Interfaces
{
    public interface IMomoTransactionService : IBaseService<MomoTransaction>
    {
        Task<MomoTransaction?> FindByRequestIdAsync(string requestId);
        Task<MomoTransaction?> FindByRequestBodyContainsAsync(string content);
    }
}
