using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxiApp.Backend.Core.Interfaces;

namespace TaxiApp.Backend.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BaseController : ControllerBase
    {
        protected readonly IUserBlockRepository userBlockRepository;
        protected readonly IUserRepository userRepository;

        public BaseController(IUserBlockRepository userBlockRepo, IUserRepository userRepo)
        {
            userBlockRepository = userBlockRepo;
            userRepository = userRepo;
        }

        // دالة للتحقق من صلاحية المستخدم
        protected async Task<IActionResult?> CheckUserAccessAsync(string userId)
        {

            if (string.IsNullOrEmpty(userId))
                return Unauthorized("لم يتم العثور على هوية المستخدم");

            var user = await userRepository.GetUserByIdAsync(userId);

            if (user.IsDeleted)
                return StatusCode(403, new { message = "هذا الحساب محذوف" });

            if (await userBlockRepository.IsUserBlocked(userId))
                return StatusCode(403, new { message = "حسابك محظور، لا يمكنك تنفيذ هذه العملية" });

            if (!await userRepository.IsUserActive(userId))
                return StatusCode(403, new { message = "حسابك غير نشط ، لا يمكنك تنفيذ هذه العملية" });

            return null;
        }
    }

}
