using System.Text.Json;
using System.Text.Json.Nodes;
using BLL.Services.Interfaces;
using Common.DTOs;
using Microsoft.Extensions.Configuration;

namespace BLL.Services.Implements;

// Thin wrapper so IHostEnvironment stays in the API project and BLL stays clean
public sealed class AppSettingsFilePath
{
    public string Path { get; }
    public AppSettingsFilePath(string path) => Path = path;
}

public class AppSettingsService : IAppSettingsService
{
    private readonly IConfiguration _configuration;
    private readonly string _appSettingsPath;

    private static readonly SemaphoreSlim _fileLock = new(1, 1);
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public AppSettingsService(IConfiguration configuration, AppSettingsFilePath pathProvider)
    {
        _configuration = configuration;
        _appSettingsPath = pathProvider.Path;
    }

    public AppSettingsDto GetSettings()
    {
        return new AppSettingsDto
        {
            BookingCheckTimeSettings = new BookingCheckTimeSettingsDto
            {
                EarlyCheckInFeePercentOfDaily = _configuration.GetValue<double>("BookingCheckTimeSettings:EarlyCheckInFeePercentOfDaily", 0.5),
                LateCheckOutFeePercentPerHour = _configuration.GetValue<double>("BookingCheckTimeSettings:LateCheckOutFeePercentPerHour", 0.025),
                LateCheckOutFeeCapPercentOfDaily = _configuration.GetValue<double>("BookingCheckTimeSettings:LateCheckOutFeeCapPercentOfDaily", 0.25),
                CorrectionWindowHours = _configuration.GetValue<int>("BookingCheckTimeSettings:CorrectionWindowHours", 24),
                TenantResponseSilenceHours = _configuration.GetValue<int>("BookingCheckTimeSettings:TenantResponseSilenceHours", 24),
                NoShowGraceHours = _configuration.GetValue<int>("BookingCheckTimeSettings:NoShowGraceHours", 4),
                MissingCheckOutGraceHours = _configuration.GetValue<int>("BookingCheckTimeSettings:MissingCheckOutGraceHours", 6),
                ClosedWithoutCheckOutHours = _configuration.GetValue<int>("BookingCheckTimeSettings:ClosedWithoutCheckOutHours", 24),
                AutomationPollIntervalSeconds = _configuration.GetValue<int>("BookingCheckTimeSettings:AutomationPollIntervalSeconds", 300),
                FeeSettlementGraceDays = _configuration.GetValue<int>("BookingCheckTimeSettings:FeeSettlementGraceDays", 3),
            },
            BookingAdmissionPolicy = new BookingAdmissionPolicyDto
            {
                GraceWindowHours = _configuration.GetValue<int>("BookingAdmissionPolicy:GraceWindowHours", 24),
                MaxSimultaneousUnpaidConfirmedBookings = _configuration.GetValue<int>("BookingAdmissionPolicy:MaxSimultaneousUnpaidConfirmedBookings", 2),
                AllowedPaymentModesWhenDebtExists = _configuration.GetSection("BookingAdmissionPolicy:AllowedPaymentModesWhenDebtExists").Get<List<string>>() ?? new List<string> { "full" },
            },
            OccupiedRoomAlternatives = new OccupiedRoomAlternativesDto
            {
                DefaultRadiusMeters = _configuration.GetValue<int>("OccupiedRoomAlternatives:DefaultRadiusMeters", 1000),
            },
            Booking = new BookingSettingsDto
            {
                OccupiedIncidentPenaltyRate = _configuration.GetValue<double>("Booking:OccupiedIncidentPenaltyRate", 1),
            },
        };
    }

    public async Task SaveSettingsAsync(AppSettingsDto dto)
    {
        await _fileLock.WaitAsync();
        try
        {
            var json = await File.ReadAllTextAsync(_appSettingsPath);
            var root = JsonNode.Parse(json)!.AsObject();

            ApplyBookingCheckTimeSettings(root, dto.BookingCheckTimeSettings);
            ApplyBookingAdmissionPolicy(root, dto.BookingAdmissionPolicy);
            ApplyOccupiedRoomAlternatives(root, dto.OccupiedRoomAlternatives);
            ApplyBookingSettings(root, dto.Booking);

            await File.WriteAllTextAsync(_appSettingsPath, root.ToJsonString(_jsonOptions));

            if (_configuration is IConfigurationRoot configRoot)
                configRoot.Reload();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private static void ApplyBookingCheckTimeSettings(JsonObject root, BookingCheckTimeSettingsDto s)
    {
        var section = EnsureSection(root, "BookingCheckTimeSettings");
        section["EarlyCheckInFeePercentOfDaily"] = s.EarlyCheckInFeePercentOfDaily;
        section["LateCheckOutFeePercentPerHour"] = s.LateCheckOutFeePercentPerHour;
        section["LateCheckOutFeeCapPercentOfDaily"] = s.LateCheckOutFeeCapPercentOfDaily;
        section["CorrectionWindowHours"] = s.CorrectionWindowHours;
        section["TenantResponseSilenceHours"] = s.TenantResponseSilenceHours;
        section["NoShowGraceHours"] = s.NoShowGraceHours;
        section["MissingCheckOutGraceHours"] = s.MissingCheckOutGraceHours;
        section["ClosedWithoutCheckOutHours"] = s.ClosedWithoutCheckOutHours;
        section["AutomationPollIntervalSeconds"] = s.AutomationPollIntervalSeconds;
        section["FeeSettlementGraceDays"] = s.FeeSettlementGraceDays;
    }

    private static void ApplyBookingAdmissionPolicy(JsonObject root, BookingAdmissionPolicyDto s)
    {
        var section = EnsureSection(root, "BookingAdmissionPolicy");
        section["GraceWindowHours"] = s.GraceWindowHours;
        section["MaxSimultaneousUnpaidConfirmedBookings"] = s.MaxSimultaneousUnpaidConfirmedBookings;
        var arr = new JsonArray();
        foreach (var mode in s.AllowedPaymentModesWhenDebtExists)
            arr.Add(mode);
        section["AllowedPaymentModesWhenDebtExists"] = arr;
    }

    private static void ApplyOccupiedRoomAlternatives(JsonObject root, OccupiedRoomAlternativesDto s)
    {
        var section = EnsureSection(root, "OccupiedRoomAlternatives");
        section["DefaultRadiusMeters"] = s.DefaultRadiusMeters;
    }

    private static void ApplyBookingSettings(JsonObject root, BookingSettingsDto s)
    {
        var section = EnsureSection(root, "Booking");
        section["OccupiedIncidentPenaltyRate"] = s.OccupiedIncidentPenaltyRate;
    }

    private static JsonObject EnsureSection(JsonObject root, string key)
    {
        if (root[key] is not JsonObject section)
        {
            section = new JsonObject();
            root[key] = section;
        }
        return section;
    }
}
