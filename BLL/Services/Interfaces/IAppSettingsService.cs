using Common.DTOs;

namespace BLL.Services.Interfaces;

public interface IAppSettingsService
{
    AppSettingsDto GetSettings();
    Task SaveSettingsAsync(AppSettingsDto dto);
}
