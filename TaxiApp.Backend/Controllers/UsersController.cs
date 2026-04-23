using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxiApp.Backend.Core.Interfaces;

namespace TaxiApp.Backend.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles ="Admin")]
    public class UsersController : ControllerBase
    {
        private readonly IUserRepository userRepository;

        public UsersController(IUserRepository userRepository)
        {
            this.userRepository = userRepository;
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAllUsers([FromQuery] int pageNumber, [FromQuery] int pageSize) 
        {
            var result = await userRepository.GetAllUsersAsync(pageNumber, pageSize);
            if (result==null)
            {
                return NotFound();
            }
            return Ok(result);

        }

        [HttpGet("GetUserById/{userId}")]
        public async Task<IActionResult> GetUserById([FromRoute] string userId)
        {
            var result = await userRepository.GetUserByIdAsync(userId);
            if (result == null)
            {
                return NotFound();
            }
            return Ok(result);

        }

        
        [HttpGet("SearchUsers")]
        public async Task<IActionResult> SearchUsersAsync([FromQuery] string search, [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
        {
            var result = await userRepository.SearchUsersAsync(search, pageNumber, pageSize);
            if (result == null)
            {
                return NotFound();
            }
            return Ok(result);

        }

        [HttpPatch("ToggleUserActive/{userId}")]
        public async Task<IActionResult> ToggleUserActive([FromRoute] string userId)
        {
            var result = await userRepository.ToggleUserActive(userId);
            if (result == null)
            {
                return NotFound();
            }
            return Ok(result);

        }

        [HttpPatch("ChangeUserRole/{userId}")]
        public async Task<IActionResult> ChangeUserRole([FromRoute]string userId, [FromQuery] string roleName)
        {
            var result = await userRepository.ChangeUserRole(userId,roleName);
            if (!result)
            {
                return BadRequest("لم يتم تغيير الدور ");
            }
            return Ok(new {message="تم تغيير الدور بنجاح"});

        }


    }
}
