using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Infrastructure.Repositories;

namespace TaxiApp.Backend.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles ="Admin")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminRepository repository;

        public AdminController(IAdminRepository repository)
        {
            this.repository = repository;
        }

        [HttpPut("edit")]
        public async Task<IActionResult> EditAdmin([FromForm] UpdateAdminProfileDto dto)
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var success = await repository.UpdateAdminProfileAsync(adminId, dto);

            if (!success)
                return BadRequest(new { message = "فشل تعديل بيانات الأدمن" });

            return Ok(new { message = "تم تعديل بيانات الأدمن بنجاح" });
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetAdminProfile()
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var profile = await repository.GetAdminProfileAsync(adminId);

            if (profile == null)
                return NotFound(new { message = "الأدمن غير موجود" });

            return Ok(new
            {
                profile.FirstName,
                profile.LastName,
                profile.PhoneNumber,
                profile.Address,
                profile.ProfilePhotoImg
            });
        }

        [HttpDelete("SoftDeleteDriver/{id}")]
        public async Task<IActionResult> SoftDeleteDriver(string id)
        {
            await repository.SoftDeleteDriverAsync(id);
            return Ok("Driver soft deleted successfully");
        }

        [HttpGet("GetAllDrivers")]
        public async Task<IActionResult> GetAllDrivers()
        {
            var drivers = await repository.GetActiveDriversAsync();
            return Ok(drivers);
        }



        [HttpPut("RestoreDriver/{id}")]
        public async Task<IActionResult> RestoreDriver(string id)
        {
            await repository.RestoreDriverAsync(id);
            return Ok("Driver restored successfully");
        }

        [HttpDelete("SoftDeletePassenger/{id}")]
        public async Task<IActionResult> SoftDeletePassenger(string id)
        {
            await repository.SoftDeletePassengerAsync(id);
            return Ok("Passenger soft deleted successfully");
        }

        [HttpGet("GetAllPassengers")]
        public async Task<IActionResult> GetAllPassengers()
        {
            var passengers = await repository.GetActivePassengersAsync();
            return Ok(passengers);
        }

        [HttpPut("RestorePassenger/{id}")]
        public async Task<IActionResult> RestorePassenger(string id)
        {
            await repository.RestorePassengerAsync(id);
            return Ok("Passenger restored successfully");
        }

        [HttpGet("profile/{id}")]
        public async Task<IActionResult> GetPassengerProfile(string id)
        {
            var profile = await repository.GetPassengerProfileAsync(id);

            if (profile == null)
                return NotFound("الراكب غير موجود أو محذوف.");

            return Ok(profile);
        }


        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders([FromQuery] OrderFilterDto filter)
        {
            var result = await repository.GetOrdersAsync(filter.Page, filter.PageSize, filter.Status, filter.Search, filter.SortBy, filter.Ascending, filter.FromDate, filter.ToDate);

            return Ok(result);
        }

        // ==========================
        // 🔹 Get Trips
        // ==========================
        [HttpGet("trips")]
        public async Task<IActionResult> GetTrips(
           TripFilterDto filter)
        {
            var result = await repository.GetTripsAsync(
                filter.Page,
        filter.PageSize,
        filter.Status,
        filter.Search,
        filter.SortBy,
        filter.Ascending,
        filter.FromDate,
        filter.ToDate
            );

            return Ok(result);
        }


        [HttpGet("top-drivers")]
        public async Task<IActionResult> GetTopDrivers(
    [FromQuery] int top = 5,
    [FromQuery] DateTime? fromDate = null,
    [FromQuery] DateTime? toDate = null)
        {
            var result = await repository.GetTopDriversAsync(top, fromDate, toDate);
            return Ok(result);
        }

    }
}
