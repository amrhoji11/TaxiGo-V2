using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Core.Models;
using TaxiApp.Backend.Infrastructure.Data;
using TaxiApp.Backend.Infrastructure.Helper;

namespace TaxiApp.Backend.Infrastructure.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly IHubContext<NotificationHub> _hub;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> userManager;

        public NotificationRepository(
            IHubContext<NotificationHub> hub,
            ApplicationDbContext context,UserManager<ApplicationUser> userManager)
        {
            _hub = hub;
            _context = context;
            this.userManager = userManager;
        }

        public async Task SendNotificationAsync(
             string userId,
             NotificationType type,
             string title,
             string body,
             int? orderId = null,
             int? tripId = null,
             object? extraData = null,
             bool saveToDb = true
         )
        {
            var user = await _context.Users.FindAsync(userId);

            if (user == null || !user.NotificationsEnabled)
                return;

            Notification? notification = null;

            if (saveToDb)
            {
                var existingNotification = await _context.Notifications
                    .FirstOrDefaultAsync(n =>
                        n.UserId == userId &&
                        n.OrderId == orderId &&
                        n.TripId == tripId &&
                        n.Type == type &&
                        !n.IsRead);

                if (existingNotification != null)
                {
                    notification = existingNotification;
                    notification.Title = title;
                    notification.Body = body;
                    notification.CreatedAt = DateTime.UtcNow;

                    _context.Notifications.Update(notification);
                }
                else
                {
                    notification = new Notification
                    {
                        UserId = userId,
                        Type = type,
                        Title = title,
                        Body = body,
                        OrderId = orderId,
                        TripId = tripId,
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    };

                    _context.Notifications.Add(notification);
                }

                await _context.SaveChangesAsync();
            }

            // 🔥 إرسال للمستخدم (SignalR)
            await _hub.Clients
                .Group($"user-{userId}")
                .SendAsync("ReceiveNotification", new
                {
                    id = notification?.NotificationId,
                    type = type.ToString(),
                    title,
                    body,
                    orderId,
                    tripId,
                    extraData,
                    createdAt = notification?.CreatedAt ?? DateTime.UtcNow
                });

            // 🔥 تحديث UI للرحلة إذا موجودة
            if (tripId.HasValue)
            {
                await _hub.Clients
                    .Group($"trip-{tripId}")
                    .SendAsync("UpdateTripStatus", new
                    {
                        orderId,
                        tripId,
                        status = type.ToString(),
                        serverTime = DateTime.UtcNow
                    });
            }
        }

        // =========================
        // 🔥 OFFICE NOTIFICATION (NEW)
        // =========================
        public async Task SendOfficeNotificationAsync(
            string officeUserId,
            NotificationType type,
            string title,
            string body,
            int? orderId = null,
            int? tripId = null,
            object? extraData = null,
            bool saveToDb = true
        )
        {
           

            Notification? notification = null;

            if (saveToDb)
            {
                notification = new Notification
                {
                    UserId = officeUserId,
                    Type = type,
                    Title = title,
                    Body = body,
                    OrderId = orderId,
                    TripId = tripId,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
            }

            // 🔥 إرسال للمكتب كمستخدم حقيقي
            await _hub.Clients
                .Group($"user-{officeUserId}")
                .SendAsync("ReceiveNotification", new
                {
                    id = notification?.NotificationId,
                    type = type.ToString(),
                    title,
                    body,
                    orderId,
                    tripId,
                    extraData,
                    createdAt = DateTime.UtcNow
                });

            // 🔥 إرسال للـ Office dashboard group (اختياري لكن مفيد)
            await _hub.Clients
                .Group(HubGroups.Office)
                .SendAsync("ReceiveNotification", new
                {
                    type = type.ToString(),
                    title,
                    body,
                    orderId,
                    tripId,
                    extraData,
                    createdAt = DateTime.UtcNow
                });
        }


        public async Task<bool> MarkAsRead(int id, string userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId);

            if (notification == null)
                return false;

            notification.IsRead = true;

            await _context.SaveChangesAsync();

            return true;
        }


        public async Task<bool> MarkAllRead(string userId)
        {
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            if (!notifications.Any())
                return true;

            foreach (var n in notifications)
                n.IsRead = true;

            await _context.SaveChangesAsync();

            return true;
        }





    }
}