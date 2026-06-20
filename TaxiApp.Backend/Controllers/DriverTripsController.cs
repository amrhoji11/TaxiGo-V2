using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Core.Models;
using TaxiApp.Backend.Infrastructure.Data;

namespace TaxiApp.Backend.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles ="Driver")]
    public class DriverTripsController : BaseController
    {
        private readonly IDriverAssignmentRepository driverAssignment;
        private readonly IOrderRepository orderRepository;
        private readonly IMapService mapService;

        public DriverTripsController(IDriverAssignmentRepository driverAssignment, IOrderRepository orderRepository, IMapService mapService, IUserBlockRepository userBlockRepository,
                                IUserRepository userRepository): base(userBlockRepository, userRepository)
        {
            this.driverAssignment = driverAssignment;
            this.orderRepository = orderRepository;
            this.mapService = mapService;
        }
        [HttpGet("active")]
        public async Task<IActionResult> GetActiveState()
        {
            var driverId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var accessCheck = await CheckUserAccessAsync(driverId);
            if (accessCheck != null) return accessCheck;

            var state = await driverAssignment.GetActiveStateAsync(driverId);
            return Ok(state);
        }

        [EnableRateLimiting("DriverActionsPolicy")]
        [HttpPost("accept-order/{orderId}")]
        public async Task<IActionResult> AcceptOrder([FromRoute] int orderId)
        {
            var driverId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var accessCheck = await CheckUserAccessAsync(driverId);
            if (accessCheck != null) return accessCheck;

            var result = await driverAssignment.DriverAcceptOrderAsync(orderId, driverId);

            if (result != "Order added to existing trip" && result != "Trip created successfully")
                return BadRequest(new { message = result });



            return Ok(result);
        }

        [EnableRateLimiting("DriverActionsPolicy")]
        [HttpPost("reject-order/{orderId}")]
        public async Task<IActionResult> RejectOrder([FromRoute]int orderId)
        {
            var driverId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var accessCheck = await CheckUserAccessAsync(driverId);
            if (accessCheck != null) return accessCheck; 

            var result = await driverAssignment.DriverRejectOrderAsync(orderId, driverId);

            if (!result.Contains("Driver"))
                return BadRequest(new { message = result });



            return Ok(result);
        }

        // Accept Full Trip (Emergency)
        [HttpPost("accept-trip/{tripId}")]
        public async Task<IActionResult> AcceptTrip([FromRoute]int tripId)
        {
            var driverId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var accessCheck = await CheckUserAccessAsync(driverId);
            if (accessCheck != null) return accessCheck;

            var result = await driverAssignment.DriverAcceptTripAsync(tripId, driverId);

            if (result != "Trip accepted")
                return BadRequest(new { message = result });



            return Ok(new { message = result });
        }


        // Reject Full Trip
        [HttpPost("reject-trip/{tripId}")]
        public async Task<IActionResult> RejectTrip([FromRoute]int tripId)
        {
            var driverId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var accessCheck = await CheckUserAccessAsync(driverId);
            if (accessCheck != null) return accessCheck;

            var result = await driverAssignment.DriverRejectTripAsync(tripId, driverId);

            if (!result.Contains("offer"))
                return BadRequest(new { message = result });


            return Ok(new { message = result });
        }


        [HttpPost("arrived/{orderId}")]
        public async Task<IActionResult> Arrived([FromRoute] int orderId)
        {
            var driverId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var accessCheck = await CheckUserAccessAsync(driverId);
            if (accessCheck != null) return accessCheck;

            var result = await driverAssignment.DriverArrivedAsync(orderId, driverId);

            if (result != "Arrived notification sent")
                return BadRequest(new { message = result });

            return Ok(result);

        }


        [HttpPost("start-trip/{tripId}")]
        public async Task<IActionResult> StartTrip([FromRoute]int tripId)
        {
            var driverId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var accessCheck = await CheckUserAccessAsync(driverId);
            if (accessCheck != null) return accessCheck;

            var result = await driverAssignment.StartTripAsync(tripId, driverId);

            if (result != "Trip started successfully")
                return BadRequest(new { message = result });



            return Ok(result);
        }


        [HttpPost("pickup/{orderId}")]
        public async Task<IActionResult> Pickup([FromRoute]int orderId)
        {
            var driverId =User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var accessCheck = await CheckUserAccessAsync(driverId);
            if (accessCheck != null) return accessCheck;

            var result = await driverAssignment.PickupAsync(driverId, orderId);

            if (result != "Pickup successful")
                return BadRequest(new { message = result });

            return Ok(new { message = result });
        }


        [HttpPost("dropoff/{orderId}")]
        public async Task<IActionResult> Dropoff([FromRoute] int orderId)
        {
            var driverId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var accessCheck = await CheckUserAccessAsync(driverId);
            if (accessCheck != null) return accessCheck;

            var result = await driverAssignment.DropoffAsync(driverId, orderId);

            if (result != "Success")
                return BadRequest(new { message = result });

            return Ok(new { message = "Passenger dropped off successfully" });
        }


        [HttpPost("cancel-trip/{tripId}")]
        public async Task<IActionResult> CancelTrip([FromRoute]int tripId,[FromBody] CancelTripDto dto )
        {
            var driverId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var accessCheck = await CheckUserAccessAsync(driverId);
            if (accessCheck != null) return accessCheck;

            var result= await driverAssignment.CancelTripByDriverAsync(tripId, driverId, dto.Reason);

            if (!result.Contains("success"))
                return BadRequest(new { message = result });

            return Ok(result);
        }

       


        [HttpPost("enter-queue")]
        public async Task<IActionResult> EnterQueue()
        {
            var driverId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var accessCheck = await CheckUserAccessAsync(driverId);
            if (accessCheck != null) return accessCheck;

            var result =  await driverAssignment.EnterQueueAsync(driverId);

            if (result != "Entered successfully")
                return BadRequest(new { message = result });

            return Ok(new { message = result });

        }
    }
}
