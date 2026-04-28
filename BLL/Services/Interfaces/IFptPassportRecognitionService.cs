using Common.DTOs;
using Microsoft.AspNetCore.Http;

namespace BLL.Services.Interfaces;

public interface IFptPassportRecognitionService
{
    Task<FptIdRecognitionResult> RecognizeAsync(IFormFile file, CancellationToken cancellationToken = default);
}