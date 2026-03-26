using System.Threading;
using System.Threading.Tasks;
using Common.DTOs;

namespace BLL.Services.Interfaces
{
    public interface IResidenceReportPdfGenerator
    {
        Task<byte[]> GenerateAsync(TemporaryResidenceReportDetailsDto details, CancellationToken cancellationToken = default);
    }
}
