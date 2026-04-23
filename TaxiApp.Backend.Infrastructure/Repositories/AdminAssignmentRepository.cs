using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Core.Models;
using TaxiApp.Backend.Infrastructure.Data;

namespace TaxiApp.Backend.Infrastructure.Repositories
{
    public class AdminAssignmentRepository : IAdminAssignmentRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly INotificationRepository _notification;

        private const string CACHE_KEY = "SYSTEM_MODE";

        public AdminAssignmentRepository(
            ApplicationDbContext context,
            IMemoryCache cache,
            INotificationRepository notification)
        {
            _context = context;
            _cache = cache;
            _notification = notification;
        }
        public async Task<string> ManualAssignDriverAsync(int orderId, string driverId)
        {

            var order = await _context.Orders
                   .Include(o => o.TripOrders)
                   .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                return "Order not found";

            // ❌ حالات ممنوعة
            if (order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled)
                return "Cannot assign this order";

            var driver = await _context.Drivers
                .Include(d => d.Vehicles)
                .FirstOrDefaultAsync(d => d.UserId == driverId);

            if (driver == null || driver.Status == DriverStatus.offline)
                return "Driver not available";

            // 🔥 إذا كان الطلب مربوط برحلة → فك الربط
            if (order.Status == OrderStatus.AssignedToTrip && order.TripOrders.Any())
            {
                var tripOrder = order.TripOrders.First();

                var trip = await _context.Trips
                    .Include(t => t.TripOrders)
                    .FirstOrDefaultAsync(t => t.TripId == tripOrder.TripId);

                if (trip != null)
                {
                    // إزالة الطلب من الرحلة
                    trip.TripOrders.Remove(tripOrder);

                    // إذا الرحلة صارت فاضية → نحذفها
                    if (!trip.TripOrders.Any())
                    {
                        _context.Trips.Remove(trip);
                    }
                }

                order.Status = OrderStatus.Pending;
            }

            // 🔥 Manual override
            order.IsManuallyAssigned = true;
            order.LastOfferedDriverId = driverId;
            order.TripOfferSentAt = DateTime.UtcNow;
            order.Status = OrderStatus.SearchingDriver;
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await SendTripOffer(order, driverId);

            return "Manually assigned";
        }



        public async Task<string> ManualAssignTripAsync(int tripId, string driverId)
        {
            var trip = await _context.Trips
       .Include(t => t.TripOrders)
       .ThenInclude(o => o.Order)
       .FirstOrDefaultAsync(t => t.TripId == tripId);

            if (trip == null)
                return "Trip not found";

            // ❌ حالات ممنوعة
            if (trip.Status == TripStatus.Completed || trip.Status == TripStatus.Cancelled || trip.Status == TripStatus.InProgress)
                return "Cannot assign this trip";

            // 🔥 حتى لو Assigned → نسمح override
            trip.IsManuallyAssigned = true;
            trip.LastOfferedDriverId = driverId;
            trip.TripOfferSentAt = DateTime.UtcNow;
            trip.Status = TripStatus.SearchingDriver;

            // 🔥 مهم: إزالة السائق الحالي إن وجد
            trip.DriverId = null;

            await _context.SaveChangesAsync();

            await SendTripOfferForWholeTrip(trip, driverId);

            return "Trip manually assigned";
        }



        public async Task<SystemMode> GetModeAsync()
        {
            if (_cache.TryGetValue(CACHE_KEY, out SystemMode mode))
                return mode;

            var settings = await _context.SystemSettings.FirstOrDefaultAsync();
            mode = settings?.Mode ?? SystemMode.Auto;

            _cache.Set(CACHE_KEY, mode, TimeSpan.FromMinutes(5));

            return mode;
        }

        public async Task<string> SetModeAsync(SystemMode mode)
        {
            var settings = await _context.SystemSettings.FirstOrDefaultAsync();

            if (settings == null)
            {
                settings = new SystemSettings { Mode = mode };
                _context.SystemSettings.Add(settings);
            }
            else
            {
                settings.Mode = mode;
            }

            await _context.SaveChangesAsync();

            // 🔥 تحديث الكاش فورًا
            _cache.Set(CACHE_KEY, mode);

            return $"System switched to {mode}";
        }

        private async Task SendTripOfferForWholeTrip(Trip trip, string driverId)
        {
            await _notification.SendNotificationAsync(
    driverId,
    NotificationType.NewTripOffer,
    "New Shared Trip",
    "You have a trip with multiple passengers",
    null,
    trip.TripId,
    new
    {
        orders = trip.TripOrders.Select(o => new
        {
            orderId = o.OrderId,
            pickup = o.Order.PickupLocation,
            dropoff = o.Order.DropoffLocation,
            passengers = o.Order.PassengerCount
        }),
        countdown = 180
    }
);





        }


        private async Task SendTripOffer(Order order, string driverId, bool isUrgent = false)
        {
            await _notification.SendNotificationAsync(
        driverId,
        NotificationType.NewTripOffer,
        isUrgent ? "طلب مستعجل 🔥" : "New Trip Request", // عنوان ديناميكي
        isUrgent ? "هذا الطلب قريب جداً منك، يرجى الاستجابة فوراً" : "You have a new trip request",
        order.OrderId,
        null,
        new
        {
            pickup = order.PickupLocation,
            dropoff = order.DropoffLocation,
            passengers = order.PassengerCount,
            countdown = 180,
            isUrgent = isUrgent // إرسال العلم للفرونت آند لتمييز الطلب (مثلاً صوت تنبيه أقوى)
        }
    );





        }


    }
}

    
