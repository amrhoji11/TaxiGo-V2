using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.Interfaces
{
    public interface INotificationRepository
    {
        Task SendNotificationAsync(
             string userId,
             NotificationType type,
             string title,
             string body,
             int? orderId = null,
             int? tripId = null,
             object? extraData = null,
             bool saveToDb = true
         );

        Task SendOfficeNotificationAsync(
             string officeUserId,
             NotificationType type,
             string title,
             string body,
             int? orderId = null,
             int? tripId = null,
             object? extraData = null,
             bool saveToDb = true
         );


        Task<bool> MarkAsRead(int id,string userId);

        Task<bool> MarkAllRead(string userId);

        Task<PagedResult<NotificationDto>> GetUserNotificationsAsync(string userId, int pageNumber, int pageSize);

    }
}
