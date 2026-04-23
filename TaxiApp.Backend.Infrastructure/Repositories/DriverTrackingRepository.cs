using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Core.Models;
using TaxiApp.Backend.Infrastructure.Data;
using TaxiApp.Backend.Infrastructure.Helper;

namespace TaxiApp.Backend.Infrastructure.Repositories
{
    public class DriverTrackingRepository : IDriverTrackingRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hub;
        private readonly ActiveTripStore _activeTripStore;

        public DriverTrackingRepository(
            ApplicationDbContext context,
            IHubContext<NotificationHub> hub,
            ActiveTripStore activeTripStore)

        {
            _context = context;
            _hub = hub;
            _activeTripStore = activeTripStore;
        }
        public async Task UpdateDriverLocationAsync(string driverId, decimal lat, decimal lng)
        {
            var driver = await _context.Drivers
         .FirstOrDefaultAsync(d => d.UserId == driverId);

            if (driver == null)
                return;

            // 1️⃣ تحديث حالة السائق الحالية (دائماً نحدثها للبحث السريع "أين السائق الآن")
            driver.LastLat = lat;
            driver.LastLng = lng;
            driver.LastSeenAt = DateTime.UtcNow;

            // 2️⃣ منطق ذكي للحفظ في جدول History (DriverLocations)
            var lastLocation = await _context.DriverLocations
                .Where(x => x.DriverId == driverId)
                .OrderByDescending(x => x.RecordedAt)
                .FirstOrDefaultAsync();

            bool shouldSave = false;

            if (lastLocation == null)
            {
                // أول مرة يسجل فيها السائق موقعاً
                shouldSave = true;
            }
            else
            {
                // حساب هل تغير الموقع فعلياً؟
                bool hasMoved = lastLocation.Lat != lat || lastLocation.Lng != lng;

                // حساب الوقت المنقضي
                var timeDiff = DateTime.UtcNow - lastLocation.RecordedAt;

                // ✅ القرار: نحفظ فقط إذا تحرك السائق "و" مرّ على الأقل 10 ثوانٍ
                // هذا يمنع تسجيل نفس النقطة 100 مرة وهو واقف، ويمنع إرهاق الداتا بيس إذا تحرك بسرعة جنونية
                if (hasMoved && timeDiff.TotalSeconds >= 10)
                {
                    shouldSave = true;
                }
            }

            if (shouldSave)
            {
                var location = new DriverLocation
                {
                    DriverId = driverId,
                    Lat = lat,
                    Lng = lng,
                    RecordedAt = DateTime.UtcNow
                };
                _context.DriverLocations.Add(location);
            }

            // حفظ التغييرات (سواء تحديث السائق أو إضافة السجل الجديد)
            await _context.SaveChangesAsync();

            // 3️⃣ البث عبر SignalR (يبقى كما هو لنحافظ على سلاسة حركة الخريطة)
            await BroadcastLocation(driverId, lat, lng);
        }

        private async Task BroadcastLocation(  string driverId,  decimal lat,  decimal lng)
        {
            // تحقق أولًا إذا كان السائق في الطابور
            var queueEntry = await _context.OfficeQueueEntries
                .Where(q => q.DriverId == driverId && q.Status == QueueStatus.InQueue)
                .FirstOrDefaultAsync();
            bool isInQueue = queueEntry != null;

            if (!isInQueue)
            {

                // إرسال للمكتب دائماً
                await _hub.Clients
    .Group(HubGroups.Office)
    .SendAsync("DriverLocationUpdated", new
    {
        driverId,
        lat,
        lng
    });
            }


            // استخدام ActiveTripStore بدل استعلام قاعدة البيانات
            if (!_activeTripStore.TryGetTrip(driverId, out int tripId))
                return;

            // إرسال للركاب فقط إذا كانت الرحلة نشطة
            await _hub.Clients
                .Group($"trip-{tripId}")
                .SendAsync("DriverLocationUpdated", new
                {
                    driverId,
                    lat,
                    lng
                });
        }
    }
}
