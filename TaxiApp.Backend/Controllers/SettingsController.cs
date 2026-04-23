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
    [Authorize]
    public class SettingsController : ControllerBase
    {
        private readonly ISettingsRepository settingsRepository;

        public SettingsController(ISettingsRepository settingsRepository)
        {
            this.settingsRepository = settingsRepository;
        }
        [HttpPut("Language")]
        public async Task<IActionResult> UpdateLanguage([FromBody] UpdateLanguageDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // استخراج هوية المستخدم من التوكن

            var success = await settingsRepository.UpdateLanguageAsync(userId, dto.Language);

            if (!success)
                return NotFound(new { message = "المستخدم غير موجود" });

            return Ok(new { message = "تم تحديث اللغة بنجاح" });
        }

        //  عرض اللغة
        [HttpGet("language")]
        public async Task<IActionResult> GetLanguage()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var language = await settingsRepository.GetLanguageAsync(userId);

            if (language == null)
                return NotFound(new { message = "المستخدم غير موجود" });

            return Ok(new { language });
        }
        // ✅ تحديث الوضع الليلي
        [HttpPut("darkmode")]
        public async Task<IActionResult> UpdateDarkMode([FromBody] UpdateDarkModeDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var success = await settingsRepository.UpdateDarkModeAsync(userId, dto.Enabled);

            if (!success)
                return NotFound(new { message = "المستخدم غير موجود" });

            return Ok(new { message = dto.Enabled ? "تم تفعيل الوضع الليلي" : "تم إيقاف الوضع الليلي" });
        }

        // ✅ عرض الوضع الليلي
        [HttpGet("darkmode")]
        public async Task<IActionResult> GetDarkMode()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var status = await settingsRepository.GetDarkModeAsync(userId);

            if (status == null)
                return NotFound(new { message = "المستخدم غير موجود" });

            return Ok(new { darkModeEnabled = status });
        }



        [HttpPut("Notifications")]
        public async Task<IActionResult> UpdateNotifications([FromBody] UpdateNotificationsDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var success = await settingsRepository.UpdateNotificationsAsync(userId, dto.Enabled);

            if (!success)
                return NotFound(new { message = "المستخدم غير موجود" });

            return Ok(new { message = dto.Enabled ? "تم تفعيل الإشعارات" : "تم إيقاف الإشعارات" });
        }

        [HttpGet("ViewNotificationsStatus")]
        public async Task<IActionResult> GetNotificationsStatus()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // استخراج هوية المستخدم من التوكن
            var status = await settingsRepository.GetNotificationsStatusAsync(userId);

            if (status == null)
                return NotFound(new { message = "المستخدم غير موجود" });

            return Ok(new { notificationsEnabled = status });
        }



        [HttpGet("ContactWithTaxiGo")]
        public IActionResult GetContactInfo()
        {
            // رقم المكتب بصيغة دولية (مثال: فلسطين 97259xxxxxxx)
            var whatsappNumber = "+970568374256";
            var defaultMessage = "مرحبا، عندي استفسار من تطبيق TaxiGo";

            // بناء رابط واتساب
            var link = $"https://wa.me/{whatsappNumber}?text={Uri.EscapeDataString(defaultMessage)}";

            return Ok(new
            {
                whatsappLink = link,
                whatsappNumber = whatsappNumber,
                defaultMessage = defaultMessage
            });
        }


       

    }
}
