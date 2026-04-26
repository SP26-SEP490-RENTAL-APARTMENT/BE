using Common.DTOs;
using Microsoft.AspNetCore.Http;

namespace BLL.Services.Interfaces;

public interface IFptIdRecognitionService
{
    Task<FptIdRecognitionResult> RecognizeAsync(IFormFile file, CancellationToken cancellationToken = default);
}
