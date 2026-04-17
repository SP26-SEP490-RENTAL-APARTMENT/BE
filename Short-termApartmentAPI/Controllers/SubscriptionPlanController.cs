using AutoMapper;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Short_termApartmentAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SubscriptionPlanController : ControllerBase
    {
        private readonly ISubscriptionPlanService _subscriptionPlanService;
        private readonly IMapper _mapper;

        public SubscriptionPlanController(ISubscriptionPlanService subscriptionPlanService, IMapper mapper)
        {
            _subscriptionPlanService = subscriptionPlanService;
            _mapper = mapper;
        }

        [HttpGet("{id:guid}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _subscriptionPlanService.GetByIdAsync(id);
            if (result == null)
            {
                return NotFound();
            }
            return Ok(_mapper.Map<SubscriptionPlanDto>(result));
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
            var (items, totalCount) = await _subscriptionPlanService.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters);
            var itemDtos = _mapper.Map<IEnumerable<SubscriptionPlanDto>>(items);
            return Ok(new { Items = itemDtos, TotalCount = totalCount });
        }

        
        [HttpGet("landlord")]
        [Authorize(Roles = "admin,landlord")]
        public async Task<IActionResult> GetAllForLandlord(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? search = null,
            [FromQuery] Dictionary<string, string>? filters = null)
        {
            var (items, totalCount) = await _subscriptionPlanService.GetAllForLandlordAsync(page, pageSize, sortBy, sortOrder, search, filters);
            var itemDtos = _mapper.Map<IEnumerable<SubscriptionPlanDto>>(items);
            return Ok(new { Items = itemDtos, TotalCount = totalCount });
        }

        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Create([FromBody] CreateSubscriptionPlanDto planDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var plan = _mapper.Map<SubscriptionPlan>(planDto);
            plan.CreatedAt = Common.Utils.VietnamTime.Now; // Or omit if handled by DB
            var created = await _subscriptionPlanService.CreateAsync(plan);
            return CreatedAtAction(nameof(GetById), new { id = created.PlanId }, _mapper.Map<SubscriptionPlanDto>(created));
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSubscriptionPlanDto planDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var plan = await _subscriptionPlanService.GetByIdAsync(id);
            if (plan == null)
            {
                return NotFound();
            }
            _mapper.Map(planDto, plan);
            await _subscriptionPlanService.UpdateAsync(plan);
            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _subscriptionPlanService.DeleteAsync(id);
            return NoContent();
        }
    }
}