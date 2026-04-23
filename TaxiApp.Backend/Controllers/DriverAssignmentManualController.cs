using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Infrastructure.Repositories;

namespace TaxiApp.Backend.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class DriverAssignmentManualController : ControllerBase
    {
        private readonly IAdminAssignmentRepository _repo;

        public DriverAssignmentManualController(IAdminAssignmentRepository repo)
        {
            _repo = repo;
        }

        [HttpPost("manual-assign-order/{orderId}")]
        public async Task<IActionResult> ManualAssignOrder([FromRoute]int orderId,[FromBody] AssignDriverDto dto)
        {
            var result = await _repo.ManualAssignDriverAsync(orderId, dto.DriverId);
            return Ok(result);
        }

        [HttpPost("manual-assign-trip/{tripId}")]
        public async Task<IActionResult> ManualAssignTrip([FromRoute]int tripId,[FromBody] AssignDriverDto dto)
        {
            var result = await _repo.ManualAssignTripAsync(tripId, dto.DriverId);
            return Ok(result);
        }

        [HttpPost("SetMode")]
        public async Task<IActionResult> SetMode([FromBody] UpdateModeDto dto)
        {
            var result = await _repo.SetModeAsync(dto.Mode);
            return Ok(result);
        }

        [HttpGet("mode")]
        public async Task<IActionResult> GetMode()
        {
            var mode = await _repo.GetModeAsync();
            return Ok(mode);
        }
    }
}
