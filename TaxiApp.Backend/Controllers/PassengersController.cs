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
    [Authorize(Roles = "Passenger")]
    public class PassengersController : BaseController
    {
        private readonly IPassengerRepository passengerRepository;

        // شلنا الـ Passenger من هون لأنها غلط
        public PassengersController(IPassengerRepository passengerRepository, IUserBlockRepository userBlockRepository, IUserRepository userRepository) : base(userBlockRepository, userRepository)
        {
            this.passengerRepository = passengerRepository;
        }

        [HttpPut("update-profile")]
        public async Task<IActionResult> UpdateProfile( [FromForm] UpdatePassengerRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var accessCheck = await CheckUserAccessAsync(userId);
            if (accessCheck != null) return accessCheck;
            // وظيفة الكنترولر فقط يستلم الطلب ويبعته للـ Repository
            var result = await passengerRepository.UpdatePassengerProfileAsync(userId, request);

            if (!result) return NotFound("المستخدم غير موجود");

            return Ok("تم تحديث البيانات بنجاح");
        }


        [HttpGet("trips-report")]
        public async Task<IActionResult> GetTripsReport([FromQuery] DateTime from, [FromQuery] DateTime to)
        {
            if (from > to)
                return BadRequest("Invalid date range");


            var passengerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var accessCheck = await CheckUserAccessAsync(passengerId);
            if (accessCheck != null) return accessCheck;

            var report = await passengerRepository.GetPassengerTripsReportAsync(passengerId, from, to);

            return Ok(report);
        }




    }
}