using AutoMapper;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Short_termApartmentAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILandlordService _landlordService;
        private readonly IRepository<User> _userRepository;
        private readonly IMapper _mapper;

        public UserController(IUserService userService, ILandlordService landlordService, IRepository<User> userRepository, IMapper mapper)
        {
            _userService = userService;
            _landlordService = landlordService;
            _userRepository = userRepository;
            _mapper = mapper;
        }


        [HttpGet("{id:guid}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _userService.GetByIdAsync(id);
            if (result == null)
            {
                return NotFound();
            }
            return Ok(_mapper.Map<UserDto>(result));
        }

        [HttpGet]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? search = null,
            [FromQuery] Dictionary<string, string>? filters = null)
        {
            var (items, totalCount) = await _userService.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters);
            var itemDtos = _mapper.Map<IEnumerable<UserDto>>(items);
            return Ok(new { Items = itemDtos, TotalCount = totalCount });
        }

        [HttpPost]
        [Authorize(Roles = "admin")]

        public async Task<IActionResult> Create([FromBody] CreateUserDto userDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var user = _mapper.Map<User>(userDto);
            user.CreatedAt = Common.Utils.VietnamTime.Now;

            var created = await _userService.CreateAsync(user);
            return CreatedAtAction(nameof(GetById), new { id = created.UserId }, _mapper.Map<UserDto>(created));
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto userDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var user = await _userService.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            _mapper.Map(userDto, user);
            await _userService.UpdateAsync(user);
            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _userService.DeleteAsync(id);
            return NoContent();
        }



        /// <summary>
        /// Get current user's own profile information
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetMyProfile()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Invalid user token." });
            }

            var user = await _userService.GetByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            var landlord = await _landlordService.GetByUserIdAsync(userId);
            var subscriptionPlanId = landlord?.CurrentPlanId;

            return Ok(new { data = _mapper.Map<UserDto>(user), subscriptionPlanId });
        }

        /// <summary>
        /// Update current user's own profile information
        /// </summary>
        [HttpPut("me")]
        [Authorize]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateMyProfileDto userDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Invalid user token." });
            }

            var user = await _userService.GetByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            // Only allow updating specific fields
            user.FullName = userDto.FullName;
            user.Phone = userDto.Phone;
            user.Sex = userDto.Sex;
            user.Birthday = userDto.Birthday;
            user.Nationality = userDto.Nationality;
            user.NationalIdCardNumber = userDto.NationalIdCardNumber;

            await _userService.UpdateAsync(user);
            return Ok(new { message = "Profile updated successfully", data = _mapper.Map<UserDto>(user) });
        }

        /// <summary>
        /// Update current user's bank profile information
        /// </summary>
        [HttpPut("me/bank-profile")]
        [Authorize]
        public async Task<IActionResult> UpdateMyBankProfile([FromBody] UpdateMyBankProfileDto bankProfileDto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Invalid user token." });
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            if (bankProfileDto.BankAccountHolderName != null)
            {
                user.BankAccountHolderName = bankProfileDto.BankAccountHolderName;
            }

            if (bankProfileDto.BankAccountNumber != null)
            {
                user.BankAccountNumber = bankProfileDto.BankAccountNumber;
            }

            if (bankProfileDto.BankName != null)
            {
                user.BankName = bankProfileDto.BankName;
            }

            if (bankProfileDto.BankBin != null)
            {
                user.BankBin = bankProfileDto.BankBin;
            }

            await _userService.UpdateAsync(user);

            // If this user has a landlord profile, also upsert the landlord payout profile
            var landlord = await _landlordService.GetByUserIdAsync(userId);
            if (landlord != null)
            {
                var upsert = new Common.DTOs.UpsertLandlordPayoutProfileRequestDto
                {
                    ReceiverName = bankProfileDto.BankAccountHolderName?.Trim(),
                    BankAccountNo = bankProfileDto.BankAccountNumber?.Trim(),
                    BankCode = bankProfileDto.BankBin?.Trim()
                };

                await _landlordService.UpsertPayoutProfileAsync(landlord.LandlordId, upsert);
            }

            return Ok(new { message = "Bank profile updated successfully", data = _mapper.Map<UserDto>(user) });
        }
    }
}