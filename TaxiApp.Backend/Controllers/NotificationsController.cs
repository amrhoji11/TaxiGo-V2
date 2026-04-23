using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Infrastructure.Data;

namespace TaxiApp.Backend.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationRepository notificationRepository;
        private readonly ApplicationDbContext context;

        public NotificationsController(INotificationRepository notificationRepository, ApplicationDbContext context)
        {
            this.notificationRepository = notificationRepository;
            this.context = context;
        }


        [HttpPatch("mark-as-read/{id}")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (userId == null)
                return Unauthorized();

            var res = await notificationRepository.MarkAsRead(id, userId);

            if (!res)
                return NotFound();

            return Ok(res);
        }

        [HttpPatch("mark-all-read")]
        public async Task<IActionResult> MarkAllRead()
        {
            var user =  User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (user == null)
            {
                return Unauthorized();
            }

            var res = await notificationRepository.MarkAllRead(user);

            return Ok(res);
        }


    }
}
