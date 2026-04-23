using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Core.Models;
using TaxiApp.Backend.Core.Settings;
using TaxiApp.Backend.Infrastructure.Data;

namespace TaxiApp.Backend.Infrastructure.Helper
{
    public class TripOfferBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        // 1️⃣ تعريف السيمفور: يسمح لخيط واحد فقط بالدخول في المرة الواحدة
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly ILogger<TripOfferBackgroundService> _logger;
        

        public TripOfferBackgroundService(IServiceProvider serviceProvider, ILogger<TripOfferBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // التنفيذ يتم بترتيب: نفذ -> انتظر انتهاء التنفيذ -> انتظر 5 ثواني -> كرر
                await ProcessPendingTripOffers(stoppingToken);
                await Task.Delay(5000, stoppingToken);
            }
        }

        private async Task ProcessPendingTripOffers(CancellationToken stoppingToken)
        {

            // 2️⃣ محاولة دخول السيمفور
            // إذا كانت هناك عملية جارية بالفعل، سينتظر هنا أو يمكنك جعله ينسحب
            if (!await _semaphore.WaitAsync(100,stoppingToken))
            {
                _logger.LogInformation("الدورة السابقة لا تزال تعمل، تخطي هذه الدورة.");
                return;
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var adminAssignmentService = scope.ServiceProvider.GetRequiredService<IAdminAssignmentRepository>();
                var driverAssignmentService = scope.ServiceProvider.GetRequiredService<IDriverAssignmentRepository>();
                var notificationRepository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
                var settings = scope.ServiceProvider.GetRequiredService<IOptions<TaxiSettings>>().Value;
                var officeUserId = settings.OfficeUserId;
                
                var mode = await adminAssignmentService.GetModeAsync();

                if (mode == SystemMode.Manual)
                {
                    _logger.LogInformation("Manual mode → skipping auto assignment");
                    return;
                }

                var now = DateTime.UtcNow;

                // =========================
                // 1️⃣ Orders Timeout
                // =========================
                var pendingOrders = await db.Orders
                    .Where(o => o.Status == OrderStatus.SearchingDriver &&
                               !o.IsManuallyAssigned &&
                                o.TripOfferSentAt != null &&
                                o.TripOfferSentAt <= now.AddSeconds(-180) &&
                                o.CreatedAt >= now.AddMinutes(-10))
                    .OrderByDescending(o => o.Priority == OrderPriority.Urgent) // 🔥 أهم سطر
    .ThenBy(o => o.TripOfferSentAt) // الأقدم أول
                    .Take(50)
                    .ToListAsync(stoppingToken);

                foreach (var order in pendingOrders)
                {
                    if (order.IsManuallyAssigned) continue;

                    try
                    {
                        await driverAssignmentService.DriverAssignAsync(order, order.LastOfferedDriverId);
                        await Task.Delay(500, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "خطأ أثناء معالجة الطلب رقم {OrderId}", order.OrderId);
                    }
                }

                // =========================
                // 2️⃣ Trips Timeout
                // =========================
                var pendingTrips = await db.Trips
                    .Include(t => t.TripOrders)
                    .ThenInclude(o => o.Order)
                    .Where(t => t.Status == TripStatus.SearchingDriver &&
                               !t.IsManuallyAssigned &&
                                t.TripOfferSentAt != null &&
                                t.TripOfferSentAt <= now.AddSeconds(-180) &&
                                t.CreatedAt >= now.AddMinutes(-10))
                   .OrderBy(t => t.TripOfferSentAt) // 🔥 الأقدم أول
                    .Take(50)
                    .ToListAsync(stoppingToken);

                foreach (var trip in pendingTrips)
                {
                    try
                    {
                        await driverAssignmentService.AssignTripEmergencyAsync(trip.TripId, trip.LastOfferedDriverId);
                        await Task.Delay(500, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "خطأ أثناء معالجة الرحلة رقم {TripId}", trip.TripId);
                    }
                }


                // 3️⃣ Monitoring Driver Delays
// =========================
var delayedOrders = await db.Orders.Include(o => o.TripOrders) // نحتاج الوصول للرحلة لجلب ID السائق
        .ThenInclude(to => to.Trip)
    .Where(o => o.Status == OrderStatus.AssignedToTrip &&
                o.ExpectedArrivalAt != null &&
                // إذا كان الوقت الحالي تجاوز الوقت المتوقع (مثلاً بـ دقيقتين سماح)
                now > o.ExpectedArrivalAt.Value.AddMinutes(2) && // تأخر أكثر من 3 دقائق مثلاً
                o.IsDelayNotified == false)
    .OrderBy(o => o.ExpectedArrivalAt) // 🔥 الأقدم (الأكثر تأخيراً) أول          
    .Take(50)
    .ToListAsync(stoppingToken);

                foreach (var order in delayedOrders)
                {
                    try
                    {
                        var delayMinutes = (int)Math.Ceiling(
    (now - order.ExpectedArrivalAt.Value).TotalMinutes
);
                        var driverId = order.TripOrders.FirstOrDefault()?.Trip?.DriverId;
                        // إشعار المكتب
                        await notificationRepository.SendOfficeNotificationAsync(
     officeUserId: officeUserId,
     type: NotificationType.DelayWarning,
     title: "تأخير في الوصول",
     body: $"الطلب رقم {order.OrderId} متأخر حوالي {delayMinutes} دقيقة",
     orderId: order.OrderId,
     tripId: order.TripOrders.FirstOrDefault()?.TripId,
     extraData: new
     {
         driverId = driverId,
         delayMinutes,
         orderId = order.OrderId
     },
     saveToDb: true
 );

                        // إشعار الراكب
                        await notificationRepository.SendNotificationAsync(order.PassengerId, NotificationType.DelayWarning,
                            "نعتذر عن التأخير", $"السائق سيتأخر بمقدار {delayMinutes} دقيقة عن الموعد.");

                       
                        if (!string.IsNullOrEmpty(driverId))
                        {
                            await notificationRepository.SendNotificationAsync(driverId, NotificationType.DelayWarning,
                                "تنبيه تأخير", $"لقد تجاوزت الوقت المتوقع للوصول بـ {delayMinutes} دقيقة. يرجى الإسراع للراكب.");
                        }

                        order.IsDelayNotified = true; // تم التنبيه، لا تكرره
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "خطأ أثناء إرسال إشعار التأخير للطلب {OrderId}", order.OrderId);
                    }
                }
                // لا تنسى حفظ التغييرات في نهاية الدورة
                await db.SaveChangesAsync(stoppingToken);
            }
            finally
            {
                // 3️⃣ تحرير السيمفور دائماً في الـ finally لضمان عدم قفل الخدمة للأبد
                _semaphore.Release();
            }
        }
    }
}
