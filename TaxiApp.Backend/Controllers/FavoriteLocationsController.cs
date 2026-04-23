using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Infrastructure.Repositories;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TaxiApp.Backend.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles ="Passenger")]
    public class FavoriteLocationsController : ControllerBase
    {
        private readonly IFavoriteLocationsRepository favoriteLocationsRepository;
        private readonly IUserRepository userRepository;

        public FavoriteLocationsController(IFavoriteLocationsRepository favoriteLocationsRepository,IUserRepository userRepository)
        {
            this.favoriteLocationsRepository = favoriteLocationsRepository;
            this.userRepository = userRepository;
        }

        // إضافة موقع جديد
        [HttpPost("AddFavoriteLocation")]
        public async Task<IActionResult> AddLocation([FromBody] AddFavoriteLocationDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
                return Unauthorized("لم يتم العثور على هوية المستخدم");

            var user = await userRepository.GetUserByIdAsync(userId);

            

            // 🔴 Soft Delete
            if (user.IsDeleted)
                return StatusCode(403, new { message = "هذا الحساب محذوف" });

            // 🔴 Not Active
            if (!user.IsActive)
                return StatusCode(403, new { message = "حسابك غير نشط" });

            

            await favoriteLocationsRepository.AddLocationAsync(userId, dto);

            return Ok(new { message = "تمت إضافة الموقع إلى المفضلة" });
        }

        // عرض المواقع المفضلة
        [HttpGet("GetAllFavoriteLocations")]
        public async Task<IActionResult> GetLocations()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
                return Unauthorized("لم يتم العثور على هوية المستخدم");

            var user = await userRepository.GetUserByIdAsync(userId);



            // 🔴 Soft Delete
            if (user.IsDeleted)
                return StatusCode(403, new { message = "هذا الحساب محذوف" });

            // 🔴 Not Active
            if (!user.IsActive)
                return StatusCode(403, new { message = "حسابك غير نشط" });


            var locations = await favoriteLocationsRepository.GetLocationsAsync(userId);

            return Ok(locations);
        }

        // حذف موقع مفضل
        [HttpDelete("DeleteFavoriteLocation/{locationId}")]
        public async Task<IActionResult> DeleteLocation(int locationId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
                return Unauthorized("لم يتم العثور على هوية المستخدم");

            var user = await userRepository.GetUserByIdAsync(userId);



            // 🔴 Soft Delete
            if (user.IsDeleted)
                return StatusCode(403, new { message = "هذا الحساب محذوف" });

            // 🔴 Not Active
            if (!user.IsActive)
                return StatusCode(403, new { message = "حسابك غير نشط" });


            var success = await favoriteLocationsRepository.DeleteLocationAsync(userId, locationId);

            if (!success)
                return NotFound(new { message = "الموقع غير موجود" });

            return Ok(new { message = "تم حذف الموقع من المفضلة" });
        }

    }
}
