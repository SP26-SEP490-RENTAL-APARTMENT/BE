using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace BLL.Services.Interfaces
{
    public interface IImageService
    {
        Task<string> UploadImageAsync(IFormFile file);
    }
}
