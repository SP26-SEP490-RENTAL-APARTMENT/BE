using AutoMapper;
using BLL.Mappings;
using BLL.Services.Interfaces;
using Common.DTOs;
using Common.Settings;
using DAL.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Short_termApartmentAPI.Controllers;
using Short_termApartmentAPI.Middlewares;
using System.Globalization;
using System.Security.Claims;

namespace Short_termApartmentAPI.IntegrationTests;

public class IdentityRecognitionControllerTests
{
    [Fact]
    public async Task UploadAndUpdateProfile_ReturnsUnauthorized_WhenTokenIsInvalid()
    {
        var controller = CreateController();
        SetUser(controller, new Claim(ClaimTypes.NameIdentifier, "not-a-guid"));

        var result = await controller.UploadAndUpdateProfile(new IdentityRecognitionUploadDto
        {
            Image = CreateImageFile()
        });

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<ApiResponse<string>>(unauthorized.Value);
        Assert.Equal("Invalid user token.", response.Message);
    }

    [Fact]
    public async Task UploadAndUpdateProfile_ReturnsBadRequest_WhenRecognitionFails()
    {
        var userId = Guid.NewGuid();
        var controller = CreateController(new IdentityRecognitionServiceStub
        {
            ExceptionToThrow = new ArgumentException("ID card not detected or image quality is too low.")
        }, null, new UserServiceStub(new User
        {
            UserId = userId,
            Email = "user@example.com"
        }));
        SetUser(controller, new Claim(ClaimTypes.NameIdentifier, userId.ToString()));

        var result = await controller.UploadAndUpdateProfile(new IdentityRecognitionUploadDto
        {
            Image = CreateImageFile()
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponse<string>>(badRequest.Value);
        Assert.Equal("ID card not detected or image quality is too low.", response.Message);
    }

    [Fact]
    public async Task UploadAndUpdateProfile_UpdatesProfile_WhenRecognitionSucceeds()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            UserId = userId,
            Email = "user@example.com"
        };

        var recognition = new FptIdRecognitionResult
        {
            Success = true,
            OverallConfidence = 0.95,
            FullName = "Nguyen Van A",
            DateOfBirth = "01/02/1995",
            IdNumber = "012345678901",
            ExpiryDate = "01/01/2030"
        };

        var userService = new UserServiceStub(user);
        var controller = CreateController(new IdentityRecognitionServiceStub
        {
            RecognitionResult = recognition
        }, null, userService);
        SetUser(controller, new Claim(ClaimTypes.NameIdentifier, userId.ToString()));

        var result = await controller.UploadAndUpdateProfile(new IdentityRecognitionUploadDto
        {
            Image = CreateImageFile()
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(ok.Value);
        Assert.Equal("Identity recognition completed and profile updated.", response.Message);

        Assert.Equal("Nguyen Van A", user.FullName);
        Assert.Equal("012345678901", user.NationalIdCardNumber);
        Assert.Equal(DateOnly.ParseExact("01/02/1995", "dd/MM/yyyy", CultureInfo.InvariantCulture), user.Birthday);
        Assert.Equal("VN", user.Nationality);
        Assert.True(user.IdentityVerified);
    }

    [Fact]
    public async Task UploadAndUpdateProfile_ReturnsBadRequest_WhenExpiryDateIsExpired()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            UserId = userId,
            Email = "user@example.com"
        };

        var recognition = new FptIdRecognitionResult
        {
            Success = true,
            OverallConfidence = 0.95,
            FullName = "Nguyen Van A",
            DateOfBirth = "01/02/1995",
            IdNumber = "012345678901",
            ExpiryDate = "01/01/2020"
        };

        var controller = CreateController(new IdentityRecognitionServiceStub
        {
            RecognitionResult = recognition
        }, null, new UserServiceStub(user));
        SetUser(controller, new Claim(ClaimTypes.NameIdentifier, userId.ToString()));

        var result = await controller.UploadAndUpdateProfile(new IdentityRecognitionUploadDto
        {
            Image = CreateImageFile()
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponse<string>>(badRequest.Value);
        Assert.Equal("Identity document has expired.", response.Message);
    }

    private static IdentityRecognitionController CreateController(
        IFptIdRecognitionService? recognitionService = null,
        IFptPassportRecognitionService? passportRecognitionService = null,
        IUserService? userService = null)
    {
        return new IdentityRecognitionController(
            recognitionService ?? new IdentityRecognitionServiceStub(),
            passportRecognitionService ?? new FptPassportRecognitionServiceStub(),
            userService ?? new UserServiceStub(new User { UserId = Guid.NewGuid(), Email = "user@example.com" }),
            CreateMapper(),
            Options.Create(new FptIdRecognitionOptions
            {
                AutoApproveConfidenceThreshold = 0.9
            }));
    }

    [Fact]
    public async Task UploadPassport_UpdatesProfile_WhenRecognitionSucceeds()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            UserId = userId,
            Email = "foreign@example.com"
        };

        var recognition = new FptIdRecognitionResult
        {
            Success = true,
            OverallConfidence = 0.85,
            PassportNumber = "P1234567",
            FullName = "John Doe",
            DateOfBirth = "1990-01-15",
            ExpiryDate = "2030-01-01"
        };

        var userService = new UserServiceStub(user);
        var controller = CreateController(new IdentityRecognitionServiceStub(), new FptPassportRecognitionServiceStub { RecognitionResult = recognition }, userService);
        SetUser(controller, new Claim(ClaimTypes.NameIdentifier, userId.ToString()));

        var result = await controller.UploadPassport(new IdentityRecognitionUploadDto
        {
            Image = CreateImageFile()
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(ok.Value);
        Assert.Equal("Passport recognition completed and profile updated.", response.Message);

        Assert.Equal("P1234567", user.Tenant?.PassportId);
        Assert.Equal("John Doe", user.FullName);
        Assert.Equal(DateOnly.ParseExact("1990-01-15", "yyyy-MM-dd", CultureInfo.InvariantCulture), user.Birthday);
        Assert.True(user.IdentityVerified);
    }

    [Fact]
    public async Task UploadPassport_ReturnsBadRequest_WhenExpiryDateIsExpired()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            UserId = userId,
            Email = "foreign@example.com"
        };

        var recognition = new FptIdRecognitionResult
        {
            Success = true,
            OverallConfidence = 0.85,
            PassportNumber = "P1234567",
            FullName = "John Doe",
            DateOfBirth = "1990-01-15",
            ExpiryDate = "2020-01-01"
        };

        var userService = new UserServiceStub(user);
        var controller = CreateController(new IdentityRecognitionServiceStub(), new FptPassportRecognitionServiceStub { RecognitionResult = recognition }, userService);
        SetUser(controller, new Claim(ClaimTypes.NameIdentifier, userId.ToString()));

        var result = await controller.UploadPassport(new IdentityRecognitionUploadDto
        {
            Image = CreateImageFile()
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponse<string>>(badRequest.Value);
        Assert.Equal("Identity document has expired.", response.Message);
    }

    private static IMapper CreateMapper()
    {
        return new MapperConfiguration(cfg => cfg.AddProfile<UserProfile>(), NullLoggerFactory.Instance).CreateMapper();
    }

    private static void SetUser(ControllerBase controller, params Claim[] claims)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };
    }

    private static IFormFile CreateImageFile()
    {
        return new FormFile(new MemoryStream(new byte[] { 1, 2, 3 }), 0, 3, "image", "front.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
    }

    private sealed class IdentityRecognitionServiceStub : IFptIdRecognitionService
    {
        public FptIdRecognitionResult? RecognitionResult { get; init; }

        public Exception? ExceptionToThrow { get; init; }

        public Task<FptIdRecognitionResult> RecognizeAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(RecognitionResult ?? new FptIdRecognitionResult
            {
                Success = true,
                OverallConfidence = 0.95,
                FullName = "Nguyen Van A",
                DateOfBirth = "01/02/1995",
                IdNumber = "012345678901"
            });
        }
    }

    private sealed class FptPassportRecognitionServiceStub : IFptPassportRecognitionService
    {
        public FptIdRecognitionResult? RecognitionResult { get; init; }

        public Exception? ExceptionToThrow { get; init; }

        public Task<FptIdRecognitionResult> RecognizeAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(RecognitionResult ?? new FptIdRecognitionResult
            {
                Success = true,
                OverallConfidence = 0.85,
                PassportNumber = "P000000",
                FullName = "Default Passport",
                DateOfBirth = "1990-01-01"
            });
        }
    }

    private sealed class UserServiceStub : IUserService
    {
        private readonly Dictionary<Guid, User> _users = new();

        public UserServiceStub(User user)
        {
            _users[user.UserId] = user;
        }

        public Task<User?> GetByIdAsync(Guid id)
        {
            _users.TryGetValue(id, out var user);
            return Task.FromResult(user);
        }

        public Task<(IEnumerable<User> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null)
        {
            return Task.FromResult(((IEnumerable<User>)_users.Values.ToList(), _users.Count));
        }

        public Task<User> CreateAsync(User entity)
        {
            _users[entity.UserId] = entity;
            return Task.FromResult(entity);
        }

        public Task UpdateAsync(User entity)
        {
            _users[entity.UserId] = entity;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id)
        {
            _users.Remove(id);
            return Task.CompletedTask;
        }
    }
}