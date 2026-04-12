using BLL.DependencyInjection;
using Common.Settings;
using Common.Utils;
using DAL.Data;
using DAL.Seeds;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MoMoApi;
using Short_termApartmentAPI.Hubs;
using Short_termApartmentAPI.Services;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder
    .Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.Converters.Add(new VietnamDateTimeJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new VietnamNullableDateTimeJsonConverter());
        options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();
builder.Services.AddHostedService<AdminAnalyticsStreamingService>();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Rental_Apartment_API", Version = "v1" });
    c.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Description =
                "JWT Authorization header using the Bearer scheme. Example: 'Bearer {token}'",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
        }
    );
    c.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer",
                    },
                },
                Array.Empty<string>()
            },
        }
    );

    var xmlFile = $"{System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile ?? string.Empty);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    c.EnableAnnotations();

    c.OperationFilter<Short_termApartmentAPI.Swagger.AuthorizeCheckOperationFilter>();
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mySqlOptions => mySqlOptions.UseNetTopologySuite())
               .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
               .AddInterceptors(new VietnamTimeZoneConnectionInterceptor());
});

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        var jwtSettings =
            builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
            ?? throw new InvalidOperationException(
                "JWT settings are not configured properly."
            );
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.Key)
            ),
        };
    });

builder.Services.AddBLLDependencies(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var app = builder.Build();

var momoConfig = app.Configuration.GetSection(MomoOptions.SectionName);
var momoIpnUrl = momoConfig["IpnUrl"] ?? string.Empty;
var momoDisbursementIpnUrl = momoConfig["DisbursementIpnUrl"] ?? string.Empty;
var momoPartnerCode = momoConfig["PartnerCode"] ?? string.Empty;

app.Logger.LogInformation(
    "Startup config: Environment={Environment}, MoMoPartnerCode={PartnerCode}, MoMoIpnUrl={IpnUrl}, MoMoDisbursementIpnUrl={DisbursementIpnUrl}",
    app.Environment.EnvironmentName,
    momoPartnerCode,
    momoIpnUrl,
    momoDisbursementIpnUrl);

if (string.IsNullOrWhiteSpace(momoIpnUrl))
{
    app.Logger.LogWarning("MoMo IPN URL is empty. Callbacks will not be delivered correctly.");
}

if (string.IsNullOrWhiteSpace(momoDisbursementIpnUrl))
{
    app.Logger.LogWarning("MoMo disbursement IPN URL is empty. Disbursement callbacks may fail.");
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    
    // Clean up any conflicting migrations from history
    try
    {
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();
        
        using (var command = connection.CreateCommand())
        {
            // Remove any pending migrations that conflict with existing tables
            command.CommandText = "DELETE FROM __EFMigrationsHistory WHERE MigrationId LIKE '20260409%';";
            await command.ExecuteNonQueryAsync();
        }
        
        // Add missing User columns if they don't exist
        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                ALTER TABLE users
                    ADD COLUMN IF NOT EXISTS token VARCHAR(500) NULL COMMENT 'User access token',
                    ADD COLUMN IF NOT EXISTS token_expired DATETIME NULL COMMENT 'Token expiration time';";
            await command.ExecuteNonQueryAsync();
            app.Logger.LogInformation("Ensured token columns exist on users table");
        }
        
        await connection.CloseAsync();
        app.Logger.LogInformation("Cleaned up conflicting migrations from history and added missing columns");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not clean migrations history or add columns (tables may not exist yet)");
    }
    
    await DbInitializer.SeedAsync(dbContext);
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("CorsPolicy");

if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{

}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<AdminAnalyticsHub>("/hubs/admin-analytics");

app.Run();

public partial class Program { }
