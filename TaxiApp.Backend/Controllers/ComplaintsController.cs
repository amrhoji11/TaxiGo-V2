using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ComplaintsController : BaseController
    {
        private readonly IComplaintRepository _repo;

        public ComplaintsController(IComplaintRepository repo, IUserBlockRepository userBlockRepository,
                                IUserRepository userRepository): base(userBlockRepository, userRepository)
        {
            _repo = repo;
        }

        [Authorize]
        [HttpPost("/api/orders/{orderId}/complaints")]
        public async Task<IActionResult> Create([FromRoute]int orderId,[FromBody] CreateComplaintDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var accessCheck = await CheckUserAccessAsync(userId);
            if (accessCheck != null) return accessCheck;

            var result = await _repo.CreateComplaintAsync(userId,orderId ,dto);

            return Ok(result);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("all")]
        public async Task<IActionResult> GetAll()
            => Ok(await _repo.GetAllComplaintsAsync());

        [Authorize(Roles = "Admin")]
        [HttpPatch("update-status/{ComplaintId}")]
        public async Task<IActionResult> Update([FromRoute] int ComplaintId,[FromBody] UpdateComplaintStatusDto dto)
            => Ok(await _repo.UpdateStatusAsync(ComplaintId, dto));

        [Authorize(Roles = "Admin")]
        [HttpGet("violations")]
        public async Task<IActionResult> Violations()
            => Ok(await _repo.GetAllViolationsAsync());

        [Authorize(Roles = "Admin")]
        [HttpGet("driver/{driverId}/violations-count")]
        public async Task<IActionResult> Count([FromRoute]string driverId)
            => Ok(await _repo.GetDriverViolationsCountAsync(driverId));

        [Authorize(Roles = "Admin")]
        [HttpPatch("violations/{id}/resolve")]
        public async Task<IActionResult> ResolveViolation([FromRoute]int id)
        {
            var result = await _repo.ResolveViolationAsync(id);
            return Ok(result);
        }
    }
}
