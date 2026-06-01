using AutoMapper;
using BLL.Services.Interfaces;
using Common.DTOs;
using Common.Settings;
using DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Short_termApartmentAPI.Middlewares;
using System.Globalization;
using System.Security.Claims;

namespace Short_termApartmentAPI.Controllers
{
    [ApiController]
    [Route("api/id-recognition")]
    [Authorize]
    public class IdentityRecognitionController : ControllerBase
    {
        private readonly IFptIdRecognitionService _idRecognitionService;
        private readonly IFptPassportRecognitionService _passportRecognitionService;
        private readonly IUserService _userService;
        private readonly IMapper _mapper;
        private readonly decimal _autoApproveConfidenceThreshold;

        public IdentityRecognitionController(
            IFptIdRecognitionService idRecognitionService,
            IFptPassportRecognitionService passportRecognitionService,
            IUserService userService,
            IMapper mapper,
            IOptions<FptIdRecognitionOptions> options)
        {
            _idRecognitionService = idRecognitionService;
            _passportRecognitionService = passportRecognitionService;
            _userService = userService;
            _mapper = mapper;
            _autoApproveConfidenceThreshold = (decimal)options.Value.AutoApproveConfidenceThreshold;
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAndUpdateProfile([FromForm] IdentityRecognitionUploadDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            var user = await _userService.GetByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new ApiResponse<string>("User not found."));
            }

            FptIdRecognitionResult recognition;
            try
            {
                recognition = await _idRecognitionService.RecognizeAsync(dto.Image);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }

            if (!TryApplyRecognitionToUser(user, recognition, out var validationError))
            {
                return BadRequest(new ApiResponse<string>(validationError));
            }

            user.IdentityVerified = recognition.OverallConfidence >= 0.75d;
            await _userService.UpdateAsync(user);

            return Ok(new ApiResponse<object>(new
            {
                Recognition = recognition,
                Profile = _mapper.Map<UserDto>(user)
            }, "Identity recognition completed and profile updated."));
        }

        private static bool TryApplyRecognitionToUser(User user, FptIdRecognitionResult recognition, out string errorMessage)
        {
            if (recognition.OverallConfidence <= 0)
            {
                errorMessage = "Identity recognition did not return confidence scores.";
                return false;
            }

            if (recognition.OverallConfidence < 0.9d)
            {
                errorMessage = "Identity recognition confidence is too low. Please upload a clearer image.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(recognition.FullName))
            {
                errorMessage = "Identity recognition did not return a full name.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(recognition.IdNumber))
            {
                errorMessage = "Identity recognition did not return an ID number.";
                return false;
            }

            if (!TryParseDateOnly(recognition.DateOfBirth, out var birthday))
            {
                errorMessage = "Identity recognition did not return a valid date of birth.";
                return false;
            }

            if (!ValidateExpiryDate(recognition.ExpiryDate, "Identity recognition", out errorMessage))
            {
                return false;
            }

            user.FullName = recognition.FullName.Trim();
            user.NationalIdCardNumber = recognition.IdNumber.Trim();
            user.Birthday = birthday;
            user.Sex = string.IsNullOrWhiteSpace(recognition.Sex) ? null : recognition.Sex.Trim();
            user.Nationality = string.IsNullOrWhiteSpace(user.Nationality) ? "VN" : user.Nationality;

            errorMessage = string.Empty;
            return true;
        }

        private static bool TryParseDateOnly(string? rawValue, out DateOnly date)
        {
            var formats = new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "d-M-yyyy" };
            if (!string.IsNullOrWhiteSpace(rawValue))
            {
                foreach (var format in formats)
                {
                    if (DateOnly.TryParseExact(rawValue, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                    {
                        return true;
                    }
                }

                if (DateOnly.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                {
                    return true;
                }
            }

            date = default;
            return false;
        }

        private static bool ValidateExpiryDate(string? rawValue, string context, out string errorMessage)
        {
            if (!TryParseDateOnly(rawValue, out var expiryDate))
            {
                errorMessage = $"{context} did not return a valid expiry date.";
                return false;
            }

            if (expiryDate < Common.Utils.VietnamTime.Today)
            {
                errorMessage = "Identity document has expired.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        [HttpPost("passport")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadPassport([FromForm] IdentityRecognitionUploadDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new ApiResponse<string>("Invalid user token."));
            }

            var user = await _userService.GetByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new ApiResponse<string>("User not found."));
            }

            FptIdRecognitionResult recognition;
            try
            {
                recognition = await _passportRecognitionService.RecognizeAsync(dto.Image);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<string>(ex.Message));
            }

            if (!TryApplyPassportRecognitionToUser(user, recognition, out var validationError))
            {
                return BadRequest(new ApiResponse<string>(validationError));
            }

            user.IdentityVerified = recognition.OverallConfidence >= 0.75d;
            await _userService.UpdateAsync(user);

            return Ok(new ApiResponse<object>(new
            {
                Recognition = recognition,
                Profile = _mapper.Map<UserDto>(user)
            }, "Passport recognition completed and profile updated."));
        }

        private static bool TryApplyPassportRecognitionToUser(User user, FptIdRecognitionResult recognition, out string errorMessage)
        {
            if (recognition.OverallConfidence <= 0)
            {
                errorMessage = "Passport recognition did not return confidence scores.";
                return false;
            }

            if (recognition.OverallConfidence < 0.75d)
            {
                errorMessage = "Passport recognition confidence is too low. Please upload a clearer image.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(recognition.PassportNumber))
            {
                errorMessage = "Passport recognition did not return a passport number.";
                return false;
            }

            if (user.Tenant == null)
            {
                user.Tenant = new Tenant { TenantId = Guid.NewGuid(), PassportId = recognition.PassportNumber.Trim() };
            }
            else
            {
                user.Tenant.PassportId = recognition.PassportNumber.Trim();
            }

            if (!string.IsNullOrWhiteSpace(recognition.FullName))
            {
                user.FullName = recognition.FullName.Trim();
            }

            if (TryParseDateOnly(recognition.DateOfBirth, out var birthday))
            {
                user.Birthday = birthday;
            }

            if (!ValidateExpiryDate(recognition.ExpiryDate, "Passport recognition", out errorMessage))
            {
                return false;
            }

            user.Nationality = string.IsNullOrWhiteSpace(user.Nationality) ? null : user.Nationality;

            errorMessage = string.Empty;
            return true;
        }
    }
}