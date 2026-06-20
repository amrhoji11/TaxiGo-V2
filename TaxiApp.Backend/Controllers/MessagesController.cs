using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.Interfaces;

namespace TaxiApp.Backend.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class MessagesController : BaseController
    {
        private readonly IMessageRepository _messageRepository;

        public MessagesController(IMessageRepository messageRepository, IUserBlockRepository userBlockRepository, IUserRepository userRepository)
            : base(userBlockRepository, userRepository)
        {
            _messageRepository = messageRepository;
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var accessCheck = await CheckUserAccessAsync(userId);
            if (accessCheck != null) return accessCheck;

            var (success, message, data) = await _messageRepository.SendMessageAsync(userId, dto);

            if (!success)
                return BadRequest(new { message });

            return Ok(data);
        }

        [HttpGet("conversation/{orderId}")]
        public async Task<IActionResult> GetConversation([FromRoute] int orderId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 30)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var accessCheck = await CheckUserAccessAsync(userId);
            if (accessCheck != null) return accessCheck;

            var (success, message, data) = await _messageRepository.GetConversationAsync(userId, orderId, pageNumber, pageSize);

            if (!success)
                return BadRequest(new { message });

            return Ok(data);
        }

        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var accessCheck = await CheckUserAccessAsync(userId);
            if (accessCheck != null) return accessCheck;

            var data = await _messageRepository.GetUserConversationsAsync(userId);
            return Ok(data);
        }
    }
}
