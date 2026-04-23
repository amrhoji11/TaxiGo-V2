using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaxiApp.Backend.Core.DTO_S.AuthDto.Requests;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Core.Models;
using TaxiApp.Backend.Infrastructure.Repositories;

namespace TaxiApp.Backend.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles ="Driver")]
    public class DriversController : BaseController
    {
        private readonly IDriverRepository driverRepository;

        public DriversController(IDriverRepository driverRepository, IUserBlockRepository userBlockRepository,
                                IUserRepository userRepository) : base(userBlockRepository, userRepository)
        {
            this.driverRepository = driverRepository;
        }

        [HttpPut("update-profile")]
        public async Task<IActionResult> UpdateProfile( [FromForm] UpdateDriverRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var accessCheck = await CheckUserAccessAsync(userId);
            if (accessCheck != null) return accessCheck;


            var result = await driverRepository.UpdateDriverProfileAsync(userId, request);

            if (!result) return NotFound("المستخدم غير موجود");

            return Ok("تم تحديث بيانات السائق بنجاح");
        }

        [HttpGet("my-trips-report")]
        public async Task<IActionResult> GetDriverTripsReport([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            if (from.HasValue && to.HasValue && from > to)
                return BadRequest("Invalid date range");

            // جلب الـ driverId من الـ JWT
            var driverId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // تحقق من صلاحية المستخدم (لو عندك Access Check)
            var accessCheck = await CheckUserAccessAsync(driverId);
            if (accessCheck != null) return accessCheck;

            // استدعاء ال Repository لجلب البيانات
            var report = await driverRepository.GetDriverTripsReportAsync(driverId, from, to);

            return Ok(report);
        }



    }
}
