using System.Collections.Generic;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.DTO_S;

namespace TaxiApp.Backend.Core.Interfaces
{
    public interface IMessageRepository
    {
        Task<(bool Success, string Message, MessageDto? Data)> SendMessageAsync(string senderId, SendMessageDto dto);

        Task<(bool Success, string Message, PagedResult<MessageDto>? Data)> GetConversationAsync(string userId, int orderId, int pageNumber, int pageSize);

        Task<List<ConversationDto>> GetUserConversationsAsync(string userId);
    }
}
