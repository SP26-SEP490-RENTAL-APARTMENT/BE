using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace BLL.Services.Interfaces
{
    public interface IIdentityDocumentUploadService
    {
        Task<string> UploadIdentityDocumentAsync(IFormFile file);
    }
}
