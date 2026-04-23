using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using TaxiApp.Backend.Core.DTO_S.AuthDto;
using TaxiApp.Backend.Core.DTO_S.AuthDto.Requests;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Core.Models;
using TaxiApp.Backend.Infrastructure.Repositories;


namespace TaxiApp.Backend.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {

        // 1. تعريف الحقل الخاص
        private readonly IAuthRepository _authRepository;
        private readonly UserManager<ApplicationUser> userManager;
       

        // 2. تمرير الـ Repository عبر الـ Constructor
        public AccountController(IAuthRepository authRepository, UserManager<ApplicationUser> userManager)
        {
            _authRepository = authRepository;
            this.userManager = userManager;
           
        }
        [HttpPost("registerPassenger")]
        public async Task<IActionResult> RegisterPassenger([FromBody] RegisterPassengerRequest request)
        {
            // استدعاء الـ Repository لتنفيذ عملية الحفظ
            var response = await _authRepository.RegisterPassengerAsync(request);

            if (response.Message.Contains("فشل"))
                return BadRequest(response);

            // الآن ستجد البيانات في قاعدة البيانات
            return Ok(response);
        }

        [HttpPost("registerDriver")]
        public async Task<IActionResult> RegisterDriver([FromBody] RegisterDriverRequest request)
        {
            // استدعاء الـ Repository لتنفيذ عملية الحفظ
            var response = await _authRepository.RegisterDriverAsync(request);

            if (response.Message.Contains("فشل"))
                return BadRequest(response);

            // الآن ستجد البيانات في قاعدة البيانات
            return Ok(response);
        }
        // 1. طلب تسجيل الدخول (يرسل الرمز للهاتف)
        [EnableRateLimiting("LoginPolicy")]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var message = await _authRepository.LoginAsync(request);
                // ملاحظة: في مرحلة التطوير الرمز يرجع في الـ Response لسهولة التجربة
                return Ok(new { Message = message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }
        // 2. التحقق من الرمز والحصول على التوكن (للمكتب والجميع)
        [EnableRateLimiting("VerifyOtpPolicy")]
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            try
            {
                var response = await _authRepository.VerifyOtpAndLoginAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                // إذا كان الرمز خطأ أو منتهي الصلاحية سيصل هنا
                return Unauthorized(new { Error = ex.Message });
            }
        }

        // 1. طلب كود التحقق للرقم الجديد
        [Authorize]
        [EnableRateLimiting("ChangePhonePolicy")]
        [HttpPost("request-change-phone")]
        public async Task<IActionResult> RequestChangePhone(  [FromBody] ChangePhoneRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var result = await _authRepository.RequestChangePhoneNumberAsync(
     userId,
     request.CountryCode,
     request.PhoneNumber
 );

            return Ok(result);
        }

        // 2. تأكيد الكود وتغيير الرقم والـ UserName فعلياً
        [Authorize]
        [EnableRateLimiting("ChangePhonePolicy")]
        [HttpPost("confirm-change-phone")]
    
        public async Task<IActionResult> ConfirmChangePhone([FromBody] ConfirmChangePhoneRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var result = await _authRepository.ConfirmChangePhoneNumberAsync(
    userId,
    request.CountryCode,
    request.PhoneNumber,
    request.Token
);

            if (!result)
                return BadRequest("فشل تغيير الرقم");

            return Ok("تم تغيير الرقم بنجاح");
        }


        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var response = await _authRepository.RefreshTokenAsync(request.RefreshToken);

            if (response == null)
                return Unauthorized(new { Message = "Invalid refresh token" });
            return Ok(response);

           
        }
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var result = await _authRepository.RevokeRefreshTokenAsync(userId, request.RefreshToken);

            if (!result)
                return BadRequest(new { Message = "Invalid or already revoked refresh token" });

            return Ok(new { Message = "Logged out successfully" });
        }
    }
}
