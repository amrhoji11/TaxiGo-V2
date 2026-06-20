using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Core.Models;
using TaxiApp.Backend.Infrastructure.Data;
using TaxiApp.Backend.Infrastructure.Helper;

namespace TaxiApp.Backend.Infrastructure.Repositories
{
    public class MessageRepository : IMessageRepository
    {
        private static readonly TripStatus[] ChatAllowedTripStatuses =
        {
            TripStatus.Assigned,
            TripStatus.DriverArrived,
            TripStatus.InProgress,
            TripStatus.Completed
        };

        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hub;
        private readonly INotificationRepository _notification;

        public MessageRepository(ApplicationDbContext context, IHubContext<NotificationHub> hub, INotificationRepository notification)
        {
            _context = context;
            _hub = hub;
            _notification = notification;
        }

        // =========================
        // SEND MESSAGE
        // =========================
        public async Task<(bool Success, string Message, MessageDto? Data)> SendMessageAsync(string senderId, SendMessageDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Body))
                return (false, "نص الرسالة مطلوب", null);

            var order = await _context.Orders
                .Include(o => o.TripOrders.OrderByDescending(to => to.AssignedAt))
                    .ThenInclude(to => to.Trip)
                .FirstOrDefaultAsync(o => o.OrderId == dto.OrderId);

            if (order == null)
                return (false, "الطلب غير موجود", null);

            var trip = order.TripOrders
                .Where(to => to.StatusInTrip != TripOrderStatus.Unassigned && to.StatusInTrip != TripOrderStatus.Cancelled)
                .OrderByDescending(to => to.AssignedAt)
                .Select(to => to.Trip)
                .FirstOrDefault(t => t != null && !string.IsNullOrEmpty(t.DriverId));

            if (trip == null)
                return (false, "لا يمكن إرسال رسائل قبل تعيين سائق لهذا الطلب", null);

            if (!ChatAllowedTripStatuses.Contains(trip.Status))
                return (false, "لا يمكن إرسال رسائل في هذه الحالة من الرحلة", null);

            string receiverId;
            if (order.PassengerId == senderId)
                receiverId = trip.DriverId!;
            else if (trip.DriverId == senderId)
                receiverId = order.PassengerId;
            else
                return (false, "غير مخول بإرسال رسالة في هذا الطلب", null);

            var message = new Message
            {
                SenderUserId = senderId,
                ReceiverUserId = receiverId,
                OrderId = order.OrderId,
                TripId = trip.TripId,
                Body = dto.Body.Trim(),
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            var sender = await _context.Users.FindAsync(senderId);

            var messageDto = new MessageDto
            {
                MessageId = message.MessageId,
                SenderUserId = senderId,
                SenderName = sender?.FullName ?? string.Empty,
                SenderProfilePhoto = sender?.ProfilePhotoImg,
                ReceiverUserId = receiverId,
                OrderId = order.OrderId,
                TripId = trip.TripId,
                Body = message.Body,
                SentAt = message.SentAt,
                IsRead = false
            };

            await _hub.Clients.Group($"user-{receiverId}").SendAsync("ReceiveMessage", messageDto);
            await _hub.Clients.Group($"trip-{trip.TripId}").SendAsync("ReceiveMessage", messageDto);

            var preview = message.Body.Length > 100 ? message.Body.Substring(0, 100) + "…" : message.Body;
            await _notification.SendNotificationAsync(
                receiverId,
                NotificationType.MessageReceived,
                "رسالة جديدة",
                preview,
                order.OrderId,
                trip.TripId
            );

            return (true, "تم إرسال الرسالة", messageDto);
        }

        // =========================
        // GET CONVERSATION (one order thread)
        // =========================
        public async Task<(bool Success, string Message, PagedResult<MessageDto>? Data)> GetConversationAsync(string userId, int orderId, int pageNumber, int pageSize)
        {
            var order = await _context.Orders
                .Include(o => o.TripOrders)
                    .ThenInclude(to => to.Trip)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                return (false, "الطلب غير موجود", null);

            var isDriverOnOrder = order.TripOrders.Any(to => to.Trip != null && to.Trip.DriverId == userId);
            if (order.PassengerId != userId && !isDriverOnOrder)
                return (false, "غير مخول بالوصول لهذه المحادثة", null);

            var query = _context.Messages
                .Where(m => m.OrderId == orderId)
                .OrderByDescending(m => m.SentAt);

            var totalCount = await query.CountAsync();

            await _context.Messages
                .Where(m => m.OrderId == orderId && m.ReceiverUserId == userId && !m.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));

            var messages = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new MessageDto
                {
                    MessageId = m.MessageId,
                    SenderUserId = m.SenderUserId,
                    SenderName = m.Sender.FullName,
                    SenderProfilePhoto = m.Sender.ProfilePhotoImg,
                    ReceiverUserId = m.ReceiverUserId,
                    OrderId = m.OrderId,
                    TripId = m.TripId,
                    Body = m.Body,
                    SentAt = m.SentAt,
                    IsRead = m.IsRead
                })
                .ToListAsync();

            var result = new PagedResult<MessageDto>
            {
                TotalCount = totalCount,
                Page = pageNumber,
                PageSize = pageSize,
                Data = messages
            };

            return (true, "OK", result);
        }

        // =========================
        // GET USER CONVERSATIONS (list)
        // =========================
        public async Task<List<ConversationDto>> GetUserConversationsAsync(string userId)
        {
            var myMessages = await _context.Messages
                .Where(m => m.OrderId != null && (m.SenderUserId == userId || m.ReceiverUserId == userId))
                .OrderByDescending(m => m.SentAt)
                .Select(m => new
                {
                    m.OrderId,
                    m.TripId,
                    m.SenderUserId,
                    m.ReceiverUserId,
                    m.Body,
                    m.SentAt,
                    m.IsRead
                })
                .ToListAsync();

            var conversations = new List<ConversationDto>();

            foreach (var group in myMessages.GroupBy(m => m.OrderId))
            {
                var last = group.First(); // already ordered by SentAt desc
                var otherUserId = last.SenderUserId == userId ? last.ReceiverUserId : last.SenderUserId;
                var unreadCount = group.Count(m => m.ReceiverUserId == userId && !m.IsRead);

                conversations.Add(new ConversationDto
                {
                    OrderId = group.Key!.Value,
                    TripId = last.TripId,
                    OtherUserId = otherUserId,
                    LastMessageBody = last.Body,
                    LastMessageSenderId = last.SenderUserId,
                    LastMessageAt = last.SentAt,
                    UnreadCount = unreadCount
                });
            }

            var otherUserIds = conversations.Select(c => c.OtherUserId).Distinct().ToList();
            var users = await _context.Users
                .Where(u => otherUserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id);

            foreach (var c in conversations)
            {
                if (users.TryGetValue(c.OtherUserId, out var u))
                {
                    c.OtherUserName = u.FullName ?? string.Empty;
                    c.OtherUserProfilePhoto = u.ProfilePhotoImg;
                }
            }

            return conversations.OrderByDescending(c => c.LastMessageAt).ToList();
        }
    }
}
