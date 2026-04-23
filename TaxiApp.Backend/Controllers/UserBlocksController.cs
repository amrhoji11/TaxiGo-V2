using Mapster;
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
    public class UserBlocksController : ControllerBase
    {
        private readonly IUserBlockRepository userBlockRepository;

        public UserBlocksController(IUserBlockRepository userBlockRepository)
        {
            this.userBlockRepository = userBlockRepository;
        }

        [HttpPatch("{userId}/ToggleUserBlock")]
        public async Task<IActionResult> ToggleUserBlock([FromRoute]string userId ,[FromBody] ToggleUserBlockDto dto )
        {
            var officeId = User.FindFirstValue("UserId");
            if (officeId == null)
            {
                return Unauthorized();

            }


            var result = await userBlockRepository.ToggleUserBlock(userId, officeId,dto);
            if (result == null)
            {
                return NotFound(new {message="User not Found"});
            }
           
            return Ok(result);
        }

        [HttpGet("GetAllBlocks")]
        public async Task<IActionResult> GetAllBlocks([FromQuery] int pageNumber, [FromQuery] int pageSize)
        {
            var result = await userBlockRepository.GetAllBlocks(pageNumber, pageSize);
            if (result==null)
            {
                return NotFound();
            }
            return Ok(result);
        }
    }
}
