using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.Interfaces;

namespace TaxiApp.Backend.Infrastructure.Helper
{
    [Authorize]
    public class NotificationHub : Hub
    {
        private readonly IDriverTrackingRepository _tracking;

        public NotificationHub(IDriverTrackingRepository tracking)
        {
            _tracking = tracking;
        }

        public async Task SendLocation(decimal lat, decimal lng)
        {
            var driverId = Context.User?
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(driverId))
                return;


            try
            {
                await _tracking.UpdateDriverLocationAsync(driverId, lat, lng);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error updating location: " + ex.Message);
                // لا تقطع الاتصال، فقط سجل الخطأ
            }
        }


        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                // إضافة المستخدم إلى مجموعة خاصة به
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    $"user-{userId}");
            }

            if (Context.User.IsInRole("Admin"))
            {
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    HubGroups.Office);
            }

            await Clients.Caller.SendAsync("ReceiveNotification", new
            {
                title = "نظام التنبيهات",
                body = "متصل بنجاح",
                isSystemMessage = true // أضف علامة لتمييزها عن طلبات الرحلات
            });

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(
                    Context.ConnectionId,
                    $"user-{userId}");
            }

            await base.OnDisconnectedAsync(exception);
        }

        // للركاب للانضمام لرحلة
        public async Task JoinTrip(int tripId)
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                $"trip-{tripId}");
        }

        // للخروج من الرحلة
        public async Task LeaveTrip(int tripId)
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                $"trip-{tripId}");
        }

      
    }
}
