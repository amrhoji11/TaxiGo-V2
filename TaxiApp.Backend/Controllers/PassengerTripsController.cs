using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Api.Controllers
{
    [Authorize(Roles ="Passenger")]
    [Route("api/[controller]")]
    [ApiController]
    public class PassengerTripsController : BaseController
    {
        private readonly IPassengerRepository passengerRepository;

        public PassengerTripsController(IPassengerRepository passengerRepository, IUserBlockRepository userBlockRepository, IUserRepository userRepository) : base(userBlockRepository, userRepository)
        {
            this.passengerRepository = passengerRepository;
        }

        [HttpPost("rate-driver")]
        public async Task<IActionResult> RateDriver([FromBody] RateDriverRequest request)
        {
            var passengerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var accessCheck = await CheckUserAccessAsync(passengerId);
            if (accessCheck != null) return accessCheck;

            var result = await passengerRepository.RateDriverAsync(
                request.OrderId,
                passengerId,
                request.Stars,
                request.Comment
            );

            if (result != "Rating submitted successfully")
                return BadRequest(new { message = result });

            return Ok(new { message = result });
        }
    }
}
