using Azure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Core.Models;
using TaxiApp.Backend.Core.Settings;
using TaxiApp.Backend.Infrastructure.Data;
using TaxiApp.Backend.Infrastructure.Helper;

namespace TaxiApp.Backend.Infrastructure.Repositories
{
    public class DriverAssignmentRepository : IDriverAssignmentRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly TaxiSettings _settings;
        private readonly IEtaCacheService _etaCache;
        private readonly INotificationRepository _notification;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ActiveTripStore _activeTripStore;
        private readonly IMapService mapService;
        private readonly IMemoryCache _cache;
        private readonly IAdminAssignmentRepository adminAssignmentRepository;
        private readonly ISettingsRepository settingsRepository;
        private readonly TripRoutingService _tripRoutingService;
        private const string CACHE_KEY = "SYSTEM_MODE";

        public DriverAssignmentRepository(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,

            IOptions<TaxiSettings> settings,
            IEtaCacheService etaCache,
            INotificationRepository notification,
            IHubContext<NotificationHub> hubContext,
            IServiceScopeFactory scopeFactory,
            ActiveTripStore activeTripStore,IMapService mapService, IMemoryCache cache,IAdminAssignmentRepository adminAssignmentRepository, ISettingsRepository settingsRepository,TripRoutingService _tripRoutingService)
        {
            _context = context;
            this.userManager = userManager;
            _settings = settings.Value;
            _etaCache = etaCache;
            _notification = notification;
            _hubContext = hubContext;
            _activeTripStore = activeTripStore;
            this.mapService = mapService;
            this._cache = cache;
            this.adminAssignmentRepository = adminAssignmentRepository;
            this.settingsRepository = settingsRepository;
            _tripRoutingService = _tripRoutingService;
        }


        // Assign Driver
        // ==========================
        public async Task<string> DriverAssignAsync(   Order order,string? excludedDriverId = null)
        {
            if (order == null)
                return "Order not found";

            var mode = await adminAssignmentRepository.GetModeAsync();

            if (mode == SystemMode.Manual)
                return "System is manual";

            if (order.IsManuallyAssigned)
                return "Manual override active";

            if (order.Priority == OrderPriority.Urgent)
                return await AssignOrderUrgentAsync(order, excludedDriverId);

            if (order.Status != OrderStatus.Pending &&
                order.Status != OrderStatus.SearchingDriver)
                return "Order not pending";

            // =========================
            // 🔥 Shared + Near drivers
            // =========================
            var sharedDrivers = await GetSharedDrivers(
                order,
                TimeSpan.FromMinutes(_settings.MaxSharedEtaMinutes),
                excludedDriverId);

            var nearDrivers = await GetNearestDrivers(
                order,
                TimeSpan.FromMinutes(_settings.MaxEtaMinutes),
                excludedDriverId);

            var candidates = sharedDrivers
                .Concat(nearDrivers)
                .Where(c => c.DriverId != excludedDriverId)
                .GroupBy(c => c.DriverId)
                .Select(g => g.First())
                .ToList();

            // =========================
            // 🔥 QUEUE يدخل كمنافس حقيقي
            // =========================
            var queueDriver = await GetNextDriverFromQueueAsync();

            if (queueDriver != null &&
                !candidates.Any(c => c.DriverId == queueDriver.UserId))
            {
                candidates.Add(new DriverCandidate
                {
                    DriverId = queueDriver.UserId,
                    Eta = TimeSpan.FromMinutes(_settings.QueueBaseEtaMinutes), // مهم جداً
                    IsQueue = true,
                    IsShared = false
                });
            }

            // =========================
            // ❌ No drivers
            // =========================
            if (!candidates.Any())
                return await AssignFromQueue(order);

            // =========================
            // 🔥 Violations
            // =========================
            var violations = await _context.Violations
                .Where(v => v.Status == ViolationStatus.Active)
                .GroupBy(v => v.DriverId)
                .Select(g => new { DriverId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.DriverId, x => x.Count);

            // =========================
            // 🔥 Score Calculation
            // =========================
            var scores = await CalculateDriversScore(candidates, order, violations);

            foreach (var c in candidates)
            {
                c.Score = scores.ContainsKey(c.DriverId)
                    ? scores[c.DriverId]
                    : double.MaxValue;
            }

            // =========================
            // 🧠 Final Selection
            // =========================
            var bestDriver = candidates
                .OrderBy(c => c.Score)
                .ThenBy(c => c.Eta)
                .FirstOrDefault();

            if (bestDriver == null)
                return "No valid driver found";

            // =========================
            // ❌ Avoid same driver spam
            // =========================
            if (bestDriver.DriverId == order.LastOfferedDriverId)
            {
                order.TripOfferSentAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return "Same driver, delay retry";
            }

            // =========================
            // 🚀 Assign
            // =========================
            order.LastOfferedDriverId = bestDriver.DriverId;
            order.TripOfferSentAt = DateTime.UtcNow;
            order.Status = OrderStatus.SearchingDriver;
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await SendTripOffer(order, bestDriver.DriverId, isUrgent: false);

            return "Driver offer sent";
        }

        private async Task<string> AssignFromQueue(Order order)
        {
            var queueDriver = await GetNextDriverFromQueueAsync();

            if (queueDriver == null)
            {
                order.Status = OrderStatus.NoDriverFound;
                order.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                await _notification.SendNotificationAsync(
                    order.PassengerId,
                    NotificationType.NoDriverFound,
                    "نعتذر منك",
                    "لا يوجد سائق متاح حالياً",
                    order.OrderId
                );

                return "No driver available";
            }

            order.LastOfferedDriverId = queueDriver.UserId;
            order.TripOfferSentAt = DateTime.UtcNow;
            order.Status = OrderStatus.SearchingDriver;
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await SendTripOffer(order, queueDriver.UserId);

            return "Driver from queue notified";
        }

        public async Task<string> AssignTripEmergencyAsync(int tripId, string? excludedDriverId = null)
        {
            var mode = await adminAssignmentRepository.GetModeAsync();

            if (mode == SystemMode.Manual)
                return "System is manual";

            // =========================
            // 🔥 جلب الرحلة مع كل البيانات المهمة
            // =========================
            var trip = await _context.Trips
                .Include(t => t.TripOrders)
                    .ThenInclude(o => o.Order)
                .FirstOrDefaultAsync(t => t.TripId == tripId);

            if (trip == null)
                return "Trip not found";

            if (trip.IsManuallyAssigned)
                return "Manual override active";

            // =========================
            // 🔥 تحديد نقطة المرجع (Reference Location)
            // =========================
            double lat, lng;

            if (!string.IsNullOrEmpty(excludedDriverId))
            {
                var lastDriver = await _context.Drivers
                    .FirstOrDefaultAsync(d => d.UserId == excludedDriverId);

                if (lastDriver != null &&
                    lastDriver.LastLat.HasValue &&
                    lastDriver.LastLng.HasValue)
                {
                    lat = (double)lastDriver.LastLat.Value;
                    lng = (double)lastDriver.LastLng.Value;
                }
                else
                {
                    // fallback إذا السائق السابق مش موجود
                    var firstOrder = trip.TripOrders
                        .Where(o => o.StatusInTrip != TripOrderStatus.DroppedOff &&
                                    o.StatusInTrip != TripOrderStatus.Cancelled)
                        .Select(o => o.Order)
                        .FirstOrDefault();

                    if (firstOrder == null)
                        return "No reference location";

                    lat = (double)firstOrder.PickupLat!;
                    lng = (double)firstOrder.PickupLng!;
                }
            }
            else
            {
                var firstOrder = trip.TripOrders
                    .Where(o => o.StatusInTrip != TripOrderStatus.DroppedOff &&
                                o.StatusInTrip != TripOrderStatus.Cancelled)
                    .Select(o => o.Order)
                    .FirstOrDefault();

                if (firstOrder == null)
                    return "No reference location";

                lat = (double)firstOrder.PickupLat!;
                lng = (double)firstOrder.PickupLng!;
            }

            // =========================
            // 🔥 حساب عدد الركاب المطلوب
            // =========================
            int totalPassengers = trip.TripOrders
                .Where(o => o.StatusInTrip == TripOrderStatus.Assigned ||
                            o.StatusInTrip == TripOrderStatus.PickedUp)
                .Sum(o => o.Order.PassengerCount);

            var activeThreshold = DateTime.UtcNow.AddMinutes(-10);

            // =========================
            // 🔥 جلب السائقين
            // =========================
            var drivers = await _context.Drivers
                .AsNoTracking()
                .Where(d =>
                    d.LastLat.HasValue &&
                    d.LastLng.HasValue &&
                    d.LastSeenAt >= activeThreshold &&
                    d.UserId != excludedDriverId &&
                    d.Status != DriverStatus.offline)
                .Include(d => d.Vehicles)
                .ToListAsync();

            // =========================
            // 🔥 الرحلات النشطة
            // =========================
            var activeTrips = await _context.Trips
                .Include(t => t.TripOrders)
                    .ThenInclude(o => o.Order)
                .Where(t => t.Status == TripStatus.Assigned || t.Status == TripStatus.InProgress)
                .ToListAsync();

            var tripMap = activeTrips
                .Where(t => t.DriverId != null)
                .GroupBy(t => t.DriverId!)
                .ToDictionary(g => g.Key, g => g.First());

            // =========================
            // 🔥 فلترة السائقين حسب المقاعد
            // =========================
            var filtered = new List<Driver>();

            foreach (var d in drivers)
            {
                var vehicle = GetActiveVehicle(d);
                if (vehicle == null) continue;

                int currentPassengers = 0;

                if (tripMap.TryGetValue(d.UserId, out var activeTrip))
                {
                    currentPassengers = activeTrip.TripOrders
                        .Where(o => o.StatusInTrip == TripOrderStatus.PickedUp ||
                                    o.StatusInTrip == TripOrderStatus.Assigned)
                        .Sum(o => o.Order.PassengerCount);
                }

                int availableSeats = vehicle.Seats - currentPassengers;

                if (availableSeats < totalPassengers)
                    continue;

                filtered.Add(d);
            }

            // =========================
            // 🔴 fallback (queue)
            // =========================
            if (!filtered.Any())
            {
                var queueDriver = await GetNextDriverFromQueueAsync();

                if (queueDriver == null)
                {
                    trip.Status = TripStatus.NoDriverFound;
                    await _context.SaveChangesAsync();

                    foreach (var tripOrder in trip.TripOrders)
                    {
                        await _notification.SendNotificationAsync(
                            tripOrder.Order.PassengerId,
                            NotificationType.NoDriverFound,
                            "نعتذر منك",
                            "تعذر العثور على سائق لإكمال رحلتك المشتركة",
                            tripOrder.OrderId,
                            trip.TripId
                        );
                    }

                    return "No driver available";
                }

                if (queueDriver.UserId == trip.LastOfferedDriverId)
                {
                    trip.TripOfferSentAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    return "Same driver, delay retry";
                }

                trip.Status = TripStatus.SearchingDriver;
                trip.DriverId = null;
                trip.TripOfferSentAt = DateTime.UtcNow;
                trip.LastOfferedDriverId = queueDriver.UserId;

                foreach (var o in trip.TripOrders)
                {
                    if (o.Order.Status != OrderStatus.AssignedToTrip)
                    {
                        o.StatusInTrip = TripOrderStatus.Assigned;
                        o.Order.Status = OrderStatus.AssignedToTrip;
                    }
                }

                await _context.SaveChangesAsync();

                await SendTripOfferForWholeTrip(trip, queueDriver.UserId);

                return "Offer sent to queue driver";
            }

            // =========================
            // 🔥 حساب ETA (Matrix مرة واحدة)
            // =========================
            var locations = filtered
                .Select(d => new DriverLocationDto(
                    (double)d.LastLat!.Value,
                    (double)d.LastLng!.Value))
                .ToList();

            var etas = await mapService.GetDistancesAsync(locations, lat, lng);

            var candidates = new List<(Driver Driver, TimeSpan Eta)>();

            for (int i = 0; i < filtered.Count; i++)
            {
                if (etas[i] == TimeSpan.MaxValue)
                    continue;

                candidates.Add((filtered[i], etas[i]));
            }

            if (!candidates.Any())
                return "No valid ETA results";

            // =========================
            // 🔥 اختيار أفضل سائق
            // =========================
            var best = candidates.OrderBy(x => x.Eta).First();
            var bestDriver = best.Driver;

            if (bestDriver.UserId == trip.LastOfferedDriverId)
            {
                trip.TripOfferSentAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return "Same driver, delay retry";
            }

            // =========================
            // 🔥 تحديث الرحلة
            // =========================
            trip.Status = TripStatus.SearchingDriver;
            trip.DriverId = null;
            trip.TripOfferSentAt = DateTime.UtcNow;
            trip.LastOfferedDriverId = bestDriver.UserId;

            foreach (var o in trip.TripOrders)
            {
                if (o.Order.Status != OrderStatus.AssignedToTrip)
                {
                    o.StatusInTrip = TripOrderStatus.Assigned;
                    o.Order.Status = OrderStatus.AssignedToTrip;
                }
            }

            await _context.SaveChangesAsync();

            await SendTripOfferForWholeTrip(trip, bestDriver.UserId);

            return "Emergency offer sent";
        }

        private async Task<string> AssignOrderUrgentAsync(Order order, string? excludedDriverId = null)
        {
           

           

            if (order == null)
                return "Order not found";

            if (order.IsManuallyAssigned)
                return "Manual override active";

            if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.SearchingDriver)
                return "Order not pending";

            double lat = (double)order.PickupLat!;
            double lng = (double)order.PickupLng!;

            int passengers = order.PassengerCount;

            var activeThreshold = DateTime.UtcNow.AddMinutes(-10);

            var drivers = await _context.Drivers.AsNoTracking()
                .Where(d =>
                    d.LastLat.HasValue &&
                    d.LastLng.HasValue &&
                    d.LastSeenAt >= activeThreshold &&
                    d.UserId != excludedDriverId &&
                    d.Status != DriverStatus.offline)
                .Select(d => new
                {
                    Driver = d,
                    Vehicle = d.Vehicles
            .Where(v => v.IsCurrent && v.IsActive)
            .FirstOrDefault()
                })
    .Where(x => x.Vehicle != null)
    .ToListAsync();

           



            var activeTrips = await _context.Trips
                .Include(t => t.TripOrders)
                .Where(t => t.Status == TripStatus.Assigned || t.Status == TripStatus.InProgress)
                .ToListAsync();

            var tripMap = activeTrips
                .Where(t => t.DriverId != null)
                .GroupBy(t => t.DriverId!)
.ToDictionary(g => g.Key, g => g.First());

            // 🔥 فلترة أولية (بدون ETA)
            var filtered = new List<Driver>();

          

            foreach (var item in drivers)
            {
                var d = item.Driver;
                var vehicle = item.Vehicle;


                int currentPassengers = 0;

                if (tripMap.TryGetValue(d.UserId, out var activeTrip))
                {
                    currentPassengers = activeTrip.TripOrders
                        .Where(o => o.StatusInTrip == TripOrderStatus.PickedUp ||
                                    o.StatusInTrip == TripOrderStatus.Assigned)
                        .Sum(o => o.Order.PassengerCount);
                }

                int availableSeats = vehicle.Seats - currentPassengers;

                if (availableSeats < passengers)
                    continue;

                filtered.Add(d);
            }

           

            // 🔴 fallback (queue)
            if (!filtered.Any())
            {
                var queueDriver = await GetNextDriverFromQueueAsync();

                if (queueDriver == null)
                {
                    order.Status = OrderStatus.NoDriverFound;
                    order.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    await _notification.SendNotificationAsync(
                        order.PassengerId,
                        NotificationType.NoDriverFound,
                        "نعتذر منك",
                        "لم يتم العثور على سائق قريب حالياً",
                        order.OrderId
                    );

                    return "No urgent driver available";
                }

                if (queueDriver.UserId == order.LastOfferedDriverId)
                {
                    order.TripOfferSentAt = DateTime.UtcNow;
                    order.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    return "Same driver, delay retry";
                }

                order.Status = OrderStatus.SearchingDriver;
                order.TripOfferSentAt = DateTime.UtcNow;
                order.LastOfferedDriverId = queueDriver.UserId;
                order.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                await SendTripOffer(order, queueDriver.UserId, isUrgent: true);

                return "Offer sent to queue driver";
            }

            // 🔥 حساب ETA (Matrix)
            var locations = filtered
                .Select(d => new DriverLocationDto(
                    (double)d.LastLat!.Value,
                    (double)d.LastLng!.Value))
                .ToList();

            var etas = await mapService.GetDistancesAsync(locations, lat, lng);

            // 🔥 دمج ETA
            var candidates = new List<(Driver Driver, TimeSpan Eta)>();

            for (int i = 0; i < filtered.Count; i++)
            {
                if (etas[i] == TimeSpan.MaxValue) continue;

                candidates.Add((filtered[i], etas[i]));
            }

            if (!candidates.Any())
                return "No valid ETA results";

            // 🔥 اختيار الأفضل
            var best = candidates.OrderBy(x => x.Eta).First();
            var bestDriver = best.Driver;

            if (bestDriver.UserId == order.LastOfferedDriverId)
            {
                order.TripOfferSentAt = DateTime.UtcNow;
                order.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return "Same driver, delay retry";
            }

            order.Status = OrderStatus.SearchingDriver;
            order.TripOfferSentAt = DateTime.UtcNow;
            order.LastOfferedDriverId = bestDriver.UserId;
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await SendTripOffer(order, bestDriver.UserId, isUrgent: true);

            return "Urgent driver offer sent";
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
        // Send Offer
        // ==========================
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

        // Shared Drivers
        // ==========================
        private async Task<List<DriverCandidate>> GetSharedDrivers(  Order order, TimeSpan maxExtraTime,  string? excludedDriverId)
        {
            var activeThreshold = DateTime.UtcNow.AddMinutes(-10);

            // =========================
            // 🔥 1. جلب الرحلات المشتركة
            // =========================
            var trips = await _context.Trips
                .AsNoTracking()
                .Where(t =>
                    (t.Status == TripStatus.Assigned || t.Status == TripStatus.InProgress) &&
                    t.Driver.Status == DriverStatus.Shared &&
                    t.Driver.LastSeenAt >= activeThreshold &&
                    t.DriverId != excludedDriverId)
                .Include(t => t.Driver)
                .ThenInclude(d => d.Vehicles)
                .Include(t => t.TripOrders)
                .ThenInclude(o => o.Order)
                .ToListAsync();

            if (!trips.Any())
                return new List<DriverCandidate>();

            var result = new List<DriverCandidate>();

            // =========================
            // 🔥 2. فلترة + Cost + Cache
            // =========================
            foreach (var trip in trips)
            {
                var vehicle = GetActiveVehicle(trip.Driver);
                if (vehicle == null)
                    continue;

                // =========================
                // 🔥 المقاعد
                // =========================
                int currentPassengers = trip.TripOrders
                    .Where(o => o.StatusInTrip == TripOrderStatus.PickedUp ||
                                o.StatusInTrip == TripOrderStatus.Assigned)
                    .Sum(o => o.Order.PassengerCount);

                int availableSeats = vehicle.Seats - currentPassengers;

                if (availableSeats < order.PassengerCount)
                    continue;

                // =========================
                // 🔥 cache key
                // =========================
                var cacheKey = $"shared-cost-{trip.DriverId}-{order.OrderId}";

                // =========================
                // 🔥 حاول تجيب من الكاش
                // =========================
                if (_etaCache.TryGet(cacheKey, out var cachedCost))
                {
                    if (cachedCost <= maxExtraTime)
                    {
                        result.Add(new DriverCandidate
                        {
                            DriverId = trip.DriverId,
                            Eta = cachedCost,
                            IsShared = true
                        });
                    }

                    continue;
                }

                // =========================
                // 🔥 احسب Cost الحقيقي
                // =========================
                int extraMinutes = await _tripRoutingService
                    .CalculateInsertionCostAsync(trip, order);

                if (extraMinutes < 0)
                    extraMinutes = 0;

                var costTime = TimeSpan.FromMinutes(extraMinutes);

                // =========================
                // ❌ إذا بيخرب الرحلة كثير
                // =========================
                if (costTime > maxExtraTime)
                    continue;

                // =========================
                // 🔥 خزّن في الكاش
                // =========================
                _etaCache.Set(
                    cacheKey,
                    costTime,
                    _settings.EtaCacheSeconds
                );

                // =========================
                // 🔥 أضف كـ candidate
                // =========================
                result.Add(new DriverCandidate
                {
                    DriverId = trip.DriverId,
                    Eta = costTime, // 🔥 هذا cost مش ETA
                    IsShared = true
                });
            }

            return result;
        }

        // Nearest Drivers
        // ==========================
        private async Task<List<DriverCandidate>> GetNearestDrivers(Order order, TimeSpan maxEta, string? excludedDriverId)
        {
            double pickupLat = (double)order.PickupLat!;
            double pickupLng = (double)order.PickupLng!;

            var activeThreshold = DateTime.UtcNow.AddMinutes(-10);

            // =========================
            // 1️⃣ جلب السائقين
            // =========================
            var drivers = await _context.Drivers
                .AsNoTracking()
                .Where(d =>
                    d.Status == DriverStatus.available &&
                    d.LastLat.HasValue &&
                    d.LastLng.HasValue &&
                    d.LastSeenAt >= activeThreshold &&
                    d.UserId != excludedDriverId)
                .Include(d => d.Vehicles)
                .ToListAsync();

            // =========================
            // 2️⃣ فلترة بالمقاعد + ترتيب ذكي
            // =========================
            var filtered = drivers
                .Where(d =>
                {
                    var vehicle = GetActiveVehicle(d);
                    return vehicle != null &&
                           vehicle.Seats >= order.PassengerCount;
                })
                .OrderByDescending(d => d.LastSeenAt) // 🔥 أحدث سائق أولاً (أدق موقع)
                .Take(10) // 🔥 تقليل الضغط على Google
                .ToList();

            if (!filtered.Any())
                return new List<DriverCandidate>();

            // =========================
            // 3️⃣ تجهيز Matrix
            // =========================
            var locations = filtered
                .Select(d => new DriverLocationDto(
                    (double)d.LastLat!.Value,
                    (double)d.LastLng!.Value))
                .ToList();

            var etas = await mapService.GetDistancesAsync(
                locations,
                pickupLat,
                pickupLng);

            // =========================
            // 4️⃣ بناء النتائج + cache
            // =========================
            var candidates = new List<DriverCandidate>();

            for (int i = 0; i < filtered.Count; i++)
            {
                var eta = etas[i];

                if (eta == TimeSpan.MaxValue)
                    continue;

                if (eta > maxEta)
                    continue;

                var driver = filtered[i];

                // 🔥 cache (اختياري لكنه مهم)
                var cacheKey = $"nearest-{driver.UserId}-{order.OrderId}";
                _etaCache.Set(cacheKey, eta, _settings.EtaCacheSeconds);

                candidates.Add(new DriverCandidate
                {
                    DriverId = driver.UserId,
                    Eta = eta,
                    IsShared = false
                });
            }

            return candidates;
        }

        // Driver Accept
        // ==========================
        public async Task<string> DriverAcceptOrderAsync(int  orderId, string driverId)
        {



            var dbOrder = await _context.Orders
          .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (dbOrder == null)
                return "Order not found";

            if (dbOrder.LastOfferedDriverId != driverId)
                return "This order is not assigned to you";

            if (dbOrder.Status != OrderStatus.SearchingDriver)
                return "Order already taken";

            var now = DateTime.UtcNow;

            if (dbOrder.TripOfferSentAt == null ||
                (now - dbOrder.TripOfferSentAt.Value).TotalSeconds > 180)
            {
                return "Offer expired";
            }

            var driver = await _context.Drivers
                .Include(d => d.Vehicles)
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == driverId);

            if (driver == null)
                return "Driver not found";

            var vehicle = GetActiveVehicle(driver);
            if (vehicle == null)
                return "Driver has no active vehicle";

            var activeTrip = await _context.Trips
                .Include(t => t.TripOrders)
                .ThenInclude(to => to.Order)
                .Where(t => t.DriverId == driverId &&
                            (t.Status == TripStatus.Assigned || t.Status == TripStatus.InProgress))
                .FirstOrDefaultAsync();

            // =========================
            // 🚀 CASE 1: عنده رحلة
            // =========================
            if (activeTrip != null)
            {
                int currentPassengers = activeTrip.TripOrders
                    .Where(o => o.StatusInTrip == TripOrderStatus.PickedUp ||
                                o.StatusInTrip == TripOrderStatus.Assigned)
                    .Sum(o => o.Order.PassengerCount);

                if (currentPassengers + dbOrder.PassengerCount > vehicle.Seats)
                    return "Not enough seats in the current trip";

                var tripOrder = new TripOrder
                {
                    TripId = activeTrip.TripId,
                    OrderId = dbOrder.OrderId,
                    AssignedAt = DateTime.UtcNow,
                    StatusInTrip = TripOrderStatus.Assigned
                };

                await _context.TripOrders.AddAsync(tripOrder);

                dbOrder.Status = OrderStatus.AssignedToTrip;

                int passengers = currentPassengers + dbOrder.PassengerCount;
                driver.Status = passengers < vehicle.Seats
                    ? DriverStatus.Shared
                    : DriverStatus.busy;

                await _context.SaveChangesAsync();

                // =========================
                // 🔥 أهم سطر في النظام كله
                // =========================
                var route = await _tripRoutingService
                    .RecalculateTripAsync(activeTrip.TripId);

                // =========================
                // 🔥 ETA من engine (مش Google مباشرة)
                // =========================
                var step = route.Steps
                    .FirstOrDefault(s => s.OrderId == dbOrder.OrderId && s.IsPickup);

                if (step != null)
                {
                    dbOrder.ExpectedArrivalAt = now.AddMinutes(step.EstimatedMinutes);
                    dbOrder.IsDelayNotified = false;
                }

                await _context.SaveChangesAsync();

                // إشعار
                await _notification.SendNotificationAsync(
                    dbOrder.PassengerId,
                    NotificationType.DriverAcceptedTrip,
                    "Driver Accepted",
                    "Driver accepted your trip",
                    dbOrder.OrderId,
                    activeTrip.TripId,
                    new
                    {
                        driverId = driverId,
                        tripId = activeTrip.TripId,
                        isShared = true,
                        driverName = driver.User.FirstName + " " + driver.User.LastName,
                        etaMinutes = step?.EstimatedMinutes ?? 0
                    }
                );

                await RemoveDriverFromQueue(driverId);

                return "Order added to existing trip";
            }

            // =========================
            // 🚀 CASE 2: لا يوجد رحلة
            // =========================
            else
            {
                dbOrder.Status = OrderStatus.AssignedToTrip;

                var tripId = await CreateTripAsync(dbOrder, driverId);

                await _context.SaveChangesAsync();

                // =========================
                // 🔥 Engine
                // =========================
                var route = await _tripRoutingService
                    .RecalculateTripAsync(tripId);

                var step = route.Steps
                    .FirstOrDefault(s => s.OrderId == dbOrder.OrderId && s.IsPickup);

                if (step != null)
                {
                    dbOrder.ExpectedArrivalAt = now.AddMinutes(step.EstimatedMinutes);
                    dbOrder.IsDelayNotified = false;
                }

                await _context.SaveChangesAsync();

                await _notification.SendNotificationAsync(
                    dbOrder.PassengerId,
                    NotificationType.DriverAcceptedTrip,
                    "Driver Accepted",
                    "Driver accepted your trip",
                    dbOrder.OrderId,
                    tripId,
                    new
                    {
                        driverId = driverId,
                        tripId = tripId,
                        isShared = false,
                        driverName = driver.User.FirstName + " " + driver.User.LastName,
                        etaMinutes = step?.EstimatedMinutes ?? 0
                    }
                );

                await RemoveDriverFromQueue(driverId);

                return "Trip created successfully";
            }
        }


       

        public async Task<string> DriverAcceptTripAsync(int tripId, string driverId)
        {
            var trip = await _context.Trips
          .Include(t => t.TripOrders)
          .ThenInclude(o => o.Order)
          .FirstOrDefaultAsync(t => t.TripId == tripId);

            if (trip == null)
                return "Trip not found";

            if (trip.LastOfferedDriverId != driverId)
                return "Not assigned to you";

            if (trip.Status != TripStatus.SearchingDriver)
                return "Trip already taken";

            var now = DateTime.UtcNow;

            if (trip.TripOfferSentAt == null ||
                (now - trip.TripOfferSentAt.Value).TotalSeconds > 180)
                return "Offer expired";

            // =========================
            // 🔥 Assign Trip
            // =========================
            trip.DriverId = driverId;
            trip.Status = TripStatus.Assigned;
            trip.AssignedAt = now;

            var driver = await _context.Drivers
                .Include(d => d.Vehicles)
                .Include(d => d.User)
                .FirstAsync(d => d.UserId == driverId);

            var vehicle = GetActiveVehicle(driver);

            int passengers = trip.TripOrders
                .Where(o => o.StatusInTrip == TripOrderStatus.Assigned ||
                            o.StatusInTrip == TripOrderStatus.PickedUp)
                .Sum(o => o.Order.PassengerCount);

            driver.Status = passengers < vehicle.Seats
                ? DriverStatus.Shared
                : DriverStatus.busy;

            await _context.SaveChangesAsync();

            _activeTripStore.SetDriverTrip(driverId, trip.TripId);

            // =========================
            // 🔥🔥🔥 CENTRAL ENGINE
            // =========================
            var route = await _tripRoutingService
                .RecalculateTripAsync(trip.TripId);

            // =========================
            // 🔥 تحديث ETA لكل طلب
            // =========================
            foreach (var tripOrder in trip.TripOrders)
            {
                var step = route.Steps
                    .FirstOrDefault(s =>
                        s.OrderId == tripOrder.OrderId && s.IsPickup);

                if (step != null)
                {
                    tripOrder.Order.ExpectedArrivalAt =
                        now.AddMinutes(step.EstimatedMinutes);

                    tripOrder.Order.IsDelayNotified = false;
                }
            }

            await _context.SaveChangesAsync();

            // =========================
            // 🔥 إشعارات الركاب
            // =========================
            foreach (var o in trip.TripOrders)
            {
                await _notification.SendNotificationAsync(
                    o.Order.PassengerId,
                    NotificationType.DriverAcceptedTrip,
                    "Driver Assigned",
                    "A driver has taken your trip",
                    o.OrderId,
                    trip.TripId,
                    new
                    {
                        driverId = driverId,
                        tripId = trip.TripId,
                        isShared = true,
                        driverName = driver.User.FirstName + " " + driver.User.LastName,
                        etaMinutes = route.Steps
                            .FirstOrDefault(s => s.OrderId == o.OrderId && s.IsPickup)?
                            .EstimatedMinutes ?? 0
                    }
                );
            }

            // =========================
            // 🔥 إشعار السائق مع ROUTE
            // =========================
            await _notification.SendNotificationAsync(
                driverId,
                NotificationType.DriverAcceptedTrip,
                "Trip Assigned",
                "Route calculated",
                null,
                trip.TripId,
                new
                {
                    driverId = driverId,
                    tripId = trip.TripId,
                    isShared = true,
                    totalPassengers = trip.TripOrders.Sum(x => x.Order.PassengerCount),

                    // 🔥 أهم شيء
                    route = route.Steps,
                    polyline = route.Polyline,
                    totalMinutes = route.TotalMinutes
                }
            );

            await RemoveDriverFromQueue(driverId);

            return "Trip accepted with smart routing";
        }
       
        public async Task<string> DriverRejectTripAsync(int tripId, string driverId)
        {

            var trip = await _context.Trips
       .Include(t => t.TripOrders)
       .ThenInclude(o => o.Order)
       .FirstOrDefaultAsync(t => t.TripId == tripId);

            if (trip == null)
                return "Trip not found";

            // =========================
            // ✅ تحقق من السائق
            // =========================
            if (trip.LastOfferedDriverId != driverId)
                return "Not assigned to you";

            if (trip.Status != TripStatus.SearchingDriver)
                return "Trip not available";

            // =========================
            // 🔥 إشعار الركاب
            // =========================
            foreach (var o in trip.TripOrders)
            {
                await _notification.SendNotificationAsync(
                    o.Order.PassengerId,
                    NotificationType.DriverRejectedTrip,
                    "Searching new driver",
                    "Driver rejected trip",
                    o.OrderId,
                    trip.TripId,
                    new
                    {
                        driverId = driverId,
                        tripId = trip.TripId,
                        reason = "DriverRejected",
                        isSearchingNewDriver = true
                    }
                );
            }

            // =========================
            // 🔥 إشعار المكتب
            // =========================
            await _notification.SendOfficeNotificationAsync(
                officeUserId: _settings.OfficeUserId,
                type: NotificationType.DriverRejectedTrip,
                title: "Driver Rejected",
                body: $"Driver {driverId} rejected Trip {trip.TripId}",
                tripId: trip.TripId,
                extraData: new
                {
                    driverId,
                    reason = "DriverRejected"
                },
                saveToDb: false
            );

            // =========================
            // 🔥 تحديث الرحلة قبل إعادة المحاولة
            // =========================
            trip.LastOfferedDriverId = null; // 🔥 مهم جداً
            trip.TripOfferSentAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // =========================
            // ❌ Auto assignment مغلق
            // =========================
            if (!_settings.EnableAutoAssignment)
                return "Auto disabled";

            // =========================
            // 🔥 إعادة توزيع ذكية
            // =========================
            return await AssignTripEmergencyAsync(tripId, driverId);
        }

        // Driver Reject
        // ==========================
        public async Task<string> DriverRejectOrderAsync(int  orderId, string driverId)
        {
            var order = await _context.Orders
       .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                return "Order not found";

            if (order.Status != OrderStatus.SearchingDriver)
                return "Order is not available for rejection";

            if (order.LastOfferedDriverId != driverId)
                return "This order is not assigned to you";

            var now = DateTime.UtcNow;

            // =========================
            // ⏱️ تحقق من انتهاء العرض
            // =========================
            if (order.TripOfferSentAt == null ||
                (now - order.TripOfferSentAt.Value).TotalSeconds > 180)
            {
                return "Offer expired";
            }

            // =========================
            // 🔥 إشعار المكتب
            // =========================
            await _notification.SendOfficeNotificationAsync(
                officeUserId: _settings.OfficeUserId,
                type: NotificationType.DriverRejectedTrip,
                title: "Driver Rejected",
                body: $"Driver {driverId} rejected order {order.OrderId}",
                orderId: order.OrderId,
                tripId: null,
                extraData: new
                {
                    driverId,
                    reason = "DriverRejected"
                },
                saveToDb: false
            );

            // =========================
            // 🔥 إشعار الراكب
            // =========================
            await _notification.SendNotificationAsync(
                order.PassengerId,
                NotificationType.DriverRejectedTrip,
                "Searching new driver",
                "Driver rejected request",
                order.OrderId,
                null,
                new
                {
                    driverId = driverId,
                    orderId = order.OrderId,
                    status = "searching_new_driver"
                }
            );

            // =========================
            // 🔥 RESET قبل إعادة التوزيع
            // =========================
            order.LastOfferedDriverId = null; // 🔥 مهم جداً
            order.TripOfferSentAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // =========================
            // ❌ Auto assignment مغلق
            // =========================
            if (!_settings.EnableAutoAssignment)
                return "Auto disabled";

            // =========================
            // 🔥 إعادة التوزيع (مع استبعاد السائق)
            // =========================
            return await DriverAssignAsync(order, driverId);


        }

        // Create Trip
        // ==========================
        private async Task<int> CreateTripAsync(Order order, string driverId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            var driver = await _context.Drivers
                .Include(d => d.Vehicles)
                .FirstAsync(d => d.UserId == driverId);

            var vehicle = GetActiveVehicle(driver);
            if (vehicle == null)
                throw new Exception("Driver has no active vehicle");

            var trip = new Trip
            {
                DriverId = driverId,
                Status = TripStatus.Assigned,
                CreatedAt = DateTime.UtcNow,
                AssignedAt = DateTime.UtcNow
            };

            await _context.Trips.AddAsync(trip);

            await _context.TripOrders.AddAsync(new TripOrder
            {
                Trip = trip,
                OrderId = order.OrderId,
                AssignedAt = DateTime.UtcNow,
                StatusInTrip = TripOrderStatus.Assigned
            });

            order.Status = OrderStatus.AssignedToTrip;

            int occupiedSeats = order.PassengerCount;

            if (occupiedSeats < vehicle.Seats)
                driver.Status = DriverStatus.Shared;
            else
                driver.Status = DriverStatus.busy;

            await _context.SaveChangesAsync();
            _activeTripStore.SetDriverTrip(driverId, trip.TripId);

            await transaction.CommitAsync();

            return trip.TripId;
        }

        // Queue
        // ==========================
        public async Task<string> EnterQueueAsync(string driverId)
        {
            var driver = await _context.Drivers.FindAsync(driverId);

            if (driver == null || driver.Status != DriverStatus.available)
                return "can not enter to queue";

            bool exists = await _context.OfficeQueueEntries
                .AnyAsync(q => q.DriverId == driverId && q.Status == QueueStatus.InQueue);

            if (exists) return "can not enter to queue because you in queue now";

            var entry = new OfficeQueueEntry
            {
                DriverId = driverId,
                Status = QueueStatus.InQueue,
                EnteredAt = DateTime.UtcNow
            };

            _context.OfficeQueueEntries.Add(entry);

            await _context.SaveChangesAsync();

            await _notification.SendOfficeNotificationAsync(
       officeUserId: _settings.OfficeUserId,
       type: NotificationType.DriverEnteredQueue,
       title: "Driver Entered Queue",
       body: $"Driver {driverId} entered the queue",
       orderId: null,
       tripId: null,
       extraData: new
       {
           driverId,
           action = "entered",
           time = entry.EnteredAt
       },
       saveToDb: true
   );

          


            return "Entered successfully";
        }

        // Helpers
        // ==========================
        private Vehicle? GetActiveVehicle(Driver driver)
        {
            return driver.Vehicles.FirstOrDefault(v => v.IsCurrent && v.IsActive);
        }

      

        private async Task<Driver?> GetNextDriverFromQueueAsync()
        {
            var entry = await _context.OfficeQueueEntries
                .Where(q => q.Status == QueueStatus.InQueue && q.Driver.Status == DriverStatus.available)
                .OrderBy(q => q.EnteredAt)
                .Include(q => q.Driver)
                .FirstOrDefaultAsync();

          

            return entry?.Driver;
        }

        private async Task<Dictionary<string, double>> CalculateDriversScore(
            List<DriverCandidate> candidates,
            Order order , Dictionary<string, int> violations)
        {
            var driverIds = candidates.Select(c => c.DriverId).Distinct().ToList();
            var now = DateTime.UtcNow;

            // =========================
            // 🔥 Trips Stats
            // =========================
            var tripStats = await _context.Trips
                .Where(t => t.DriverId != null && driverIds.Contains(t.DriverId))
                .GroupBy(t => t.DriverId)
                .Select(g => new
                {
                    DriverId = g.Key,
                    LastTrip = g.Max(t => t.CreatedAt),
                    TripsToday = g.Count(t => t.CreatedAt.Date == now.Date)
                })
                .ToDictionaryAsync(x => x.DriverId);

            // =========================
            // ⭐ Ratings
            // =========================
            var ratings = await _context.Ratings
                .Where(r => driverIds.Contains(r.TargetUserId))
                .GroupBy(r => r.TargetUserId)
                .Select(g => new
                {
                    DriverId = g.Key,
                    Avg = g.Average(x => x.Stars)
                })
                .ToDictionaryAsync(x => x.DriverId);

            // =========================
            // 🔥 Active Trips (للـ Shared)
            // =========================
            var activeTrips = await _context.Trips
                .Include(t => t.TripOrders)
                .ThenInclude(o => o.Order)
                .Where(t => t.DriverId != null &&
                       driverIds.Contains(t.DriverId) &&
                       (t.Status == TripStatus.Assigned || t.Status == TripStatus.InProgress))
                .ToDictionaryAsync(t => t.DriverId);

            var result = new Dictionary<string, double>();

            foreach (var c in candidates)
            {
                // =========================
                // 📊 Basic Data
                // =========================
                double etaMinutes = c.Eta.TotalMinutes;

                double waiting = 0;
                int tripsToday = 0;

                if (tripStats.ContainsKey(c.DriverId))
                {
                    waiting = (now - tripStats[c.DriverId].LastTrip).TotalMinutes;
                    tripsToday = tripStats[c.DriverId].TripsToday;
                }

                double rating = ratings.ContainsKey(c.DriverId)
                    ? ratings[c.DriverId].Avg
                    : 5;

                int violationCount = violations.ContainsKey(c.DriverId)
                    ? violations[c.DriverId]
                    : 0;

                // =========================
                // 🔥 INSERTION COST (للـ Shared فقط)
                // =========================
                double insertionCost = 0;

                if (c.IsShared && activeTrips.ContainsKey(c.DriverId))
                {
                    insertionCost = await _tripRoutingService
                        .CalculateInsertionCostAsync(activeTrips[c.DriverId], order);
                }

                // =========================
                // 🔥 LOAD PENALTY (عدالة)
                // =========================
                double loadPenalty = Math.Log(tripsToday + 1) * 8;

                // =========================
                // 🔥 QUEUE BOOST (ديناميكي 🔥🔥)
                // =========================
                double queueBoost = 0;

                if (c.IsQueue == true)
                {
                    // كل ما انتظر أكثر → يصير أقوى
                    queueBoost = Math.Min(waiting * 0.5, _settings.MaxQueueBoost);
                }

                // =========================
                // 🧠 FINAL SCORE
                // =========================
                double score =
                    (etaMinutes * 3) +
                    (insertionCost * 2) +
                    (violationCount * 10) -

                    (waiting * 0.3) -
                    (rating * 5) -
                    loadPenalty -
                    queueBoost;

                result[c.DriverId] = score;
            }

            return result;
        }
        public async Task<string>DriverArrivedAsync( int orderId, string driverId)
        {
            var trip = await _context.Trips.Include(t => t.Driver).ThenInclude(d => d.User)
     // ✅ البحث عن الرحلة بغض النظر عن حالتها طالما السائق نشط عليها
     .Where(t => t.DriverId == driverId && (t.Status == TripStatus.Assigned || t.Status == TripStatus.InProgress) && t.TripOrders.Any(o => o.OrderId == orderId))
     .Include(t => t.TripOrders)
     .ThenInclude(o => o.Order)
     .FirstOrDefaultAsync();

            if (trip == null)
                return "Trip not found";

            // ✅ تحقق إنو السائق هو نفسه
            if (trip.DriverId != driverId)
                return "Unauthorized driver";

            // ✅ تحقق من حالة الرحلة
            if (trip.Status != TripStatus.Assigned && trip.Status != TripStatus.InProgress)
                return "Trip not in valid state";

            var tripOrder = await _context.TripOrders
    .Include(to => to.Order)
    .Include(to => to.Trip)
        .ThenInclude(t => t.Driver)
            .ThenInclude(d => d.User)
    .FirstOrDefaultAsync(to =>
        to.OrderId == orderId &&
        to.Trip.DriverId == driverId &&
        (to.Trip.Status == TripStatus.Assigned || to.Trip.Status == TripStatus.InProgress)
    );

            if (tripOrder == null)
                return "Order not in this trip";

            // (اختياري) منع التكرار
            if (tripOrder.StatusInTrip == TripOrderStatus.DroppedOff)
                return "Order already completed";

            if (tripOrder.StatusInTrip == TripOrderStatus.DriverArrived)
                return "Already marked as arrived";

            tripOrder.StatusInTrip = TripOrderStatus.DriverArrived;

            await _context.SaveChangesAsync();

            await _notification.SendNotificationAsync(
     tripOrder.Order.PassengerId,
     NotificationType.DriverArrived,
     "Driver Arrived",
     "Your driver has arrived",
     orderId,
     trip.TripId,
     extraData: new
     {
         driverName =trip.Driver.User.FirstName +" "+ trip.Driver.User.LastName,
         destination = tripOrder.Order.DropoffLocation
     }
 );

            return "Arrived notification sent";
        }

        public async Task<string> StartTripAsync(int tripId, string driverId)
        {
            var trip = await _context.Trips.Include(t=>t.Driver).ThenInclude(t=>t.User)
          .Include(t => t.TripOrders)
          .ThenInclude(o => o.Order)
          .FirstOrDefaultAsync(t =>
              t.TripId == tripId &&
              t.DriverId == driverId);


            if (trip == null)
                return "Trip not found or unauthorized";

            if (trip.Status != TripStatus.Assigned)
                return "Trip cannot be started";

            trip.Status = TripStatus.InProgress;
            trip.StartTime = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            foreach (var o in trip.TripOrders)
            {
                await _notification.SendNotificationAsync(
             o.Order.PassengerId,
             NotificationType.TripStarted,
             "Trip Started",
             "Your trip has started",
             o.OrderId,
             tripId,
             extraData: new
             {
                 driverName = trip.Driver.User.FirstName + " " + trip.Driver.User.LastName,
                 destination = o.Order.DropoffLocation
             }
         );
            }

            return "Trip started successfully";
        }


        public async Task<string> PickupAsync(string driverId, int orderId)
        {
            var trip = await _context.Trips.Include(t=>t.Driver).ThenInclude(t=>t.User)
                .Include(t => t.TripOrders)
                .ThenInclude(o => o.Order)
                .FirstOrDefaultAsync(t =>
                    t.DriverId == driverId &&
                   ( t.Status == TripStatus.InProgress || t.Status == TripStatus.Assigned) && t.TripOrders.Any(o => o.OrderId == orderId));

            if (trip == null)
                return "No active trip found";



            var tripOrder = await _context.TripOrders
   .Include(to => to.Order)
   .Include(to => to.Trip)
       .ThenInclude(t => t.Driver)
           .ThenInclude(d => d.User)
   .FirstOrDefaultAsync(to =>
       to.OrderId == orderId &&
       to.Trip.DriverId == driverId &&
       (to.Trip.Status == TripStatus.Assigned || to.Trip.Status == TripStatus.InProgress)
   );




            if (tripOrder == null)
                return "Order not found in this trip";

            tripOrder.Order.ExpectedArrivalAt = null;
            tripOrder.Order.IsDelayNotified = false;

            if (tripOrder.StatusInTrip != TripOrderStatus.Assigned &&
    tripOrder.StatusInTrip != TripOrderStatus.DriverArrived)
            {
                return "Order already picked up or invalid state";
            }

            if (trip.Status == TripStatus.Assigned)
            {
                trip.Status = TripStatus.InProgress;
                trip.StartTime = DateTime.UtcNow;
            }

            tripOrder.StatusInTrip = TripOrderStatus.PickedUp;


            var driver = await _context.Drivers
                .Include(d => d.Vehicles)
                .FirstOrDefaultAsync(d => d.UserId == driverId);

            if (driver == null)
                return "Driver not found";

            var vehicle = GetActiveVehicle(driver);

            if (vehicle == null)
                return "Vehicle not found";

            // ✅ حساب الركاب داخل السيارة فقط
            int passengers = trip.TripOrders
                .Where(o => o.StatusInTrip == TripOrderStatus.PickedUp)
                .Sum(o => o.Order.PassengerCount);

            // ✅ تحديث حالة السائق
            if (passengers < vehicle.Seats)
                driver.Status = DriverStatus.Shared;
            else
                driver.Status = DriverStatus.busy;

            await _context.SaveChangesAsync();

            if (trip.TripOrders.Count > 1)
            {
                await _tripRoutingService.RecalculateTripAsync(trip.TripId);
            }

            // ✅ إشعار الراكب
            await _notification.SendNotificationAsync(
        tripOrder.Order.PassengerId,
        NotificationType.PickedUp,
        "Pickup Done",
        "You are now in the trip",
        orderId,
        trip.TripId,
        extraData: new
        {
            driverName = driver.User.FirstName + " " + driver.User.LastName,
            destination = tripOrder.Order.DropoffLocation
        }
    );

            // إشعار للسائق لإظهار الوجهة على خريطته
            await _notification.SendNotificationAsync(
                driver.UserId,
                NotificationType.TripStarted, // نوع جديد لإبلاغ السائق ببدء التحرك للوجهة
                "بدء التوصيل",
                $"وجهة الراكب: {tripOrder.Order.DropoffLocation}",
                orderId,
                trip.TripId,
                extraData: new
                {
                    destLat = tripOrder.Order.DropoffLat,
                    destLng = tripOrder.Order.DropoffLng,
                    destName = tripOrder.Order.DropoffLocation
                }
            );

            return "Pickup successful";
        }


        public async Task<string> DropoffAsync(string driverId, int orderId)
        {
            var trip = await _context.Trips
                .Include(t => t.TripOrders)
                .ThenInclude(o => o.Order)
                .FirstOrDefaultAsync(t =>
            t.DriverId == driverId &&
            t.Status == TripStatus.InProgress);

            if (trip == null)
                return "No active trip found";

            var tripOrder = trip.TripOrders
                .FirstOrDefault(o => o.OrderId == orderId);

            if (tripOrder == null)
                return "Order not found in this trip";

            if (tripOrder.StatusInTrip != TripOrderStatus.PickedUp)
                return "Order is not picked up yet";

            tripOrder.StatusInTrip = TripOrderStatus.DroppedOff;
            tripOrder.Order.Status = OrderStatus.Completed; // ✅ تحديث حالة الطلب الأصلي



            var driver = await _context.Drivers
                .Include(d => d.Vehicles)
                .FirstOrDefaultAsync(d => d.UserId == trip.DriverId);

            if (driver == null)
                return "Driver not found";

            int passengers = trip.TripOrders
     .Where(o => o.StatusInTrip == TripOrderStatus.PickedUp)
     .Sum(o => o.Order.PassengerCount);

            var vehicle = GetActiveVehicle(driver);

            if (vehicle == null)
                return "Vehicle not found";


            // التحقق من أن كل الطلبات في هذه الرحلة انتهت فعلياً
            bool allOrdersFinished = trip.TripOrders.All(o =>
                o.StatusInTrip == TripOrderStatus.DroppedOff ||
                o.StatusInTrip == TripOrderStatus.Cancelled);

           

            if (allOrdersFinished)
            {
                driver.Status = DriverStatus.available;
                trip.Status = TripStatus.Completed;
                trip.CompletedAt = DateTime.UtcNow;
                trip.EndTime = DateTime.UtcNow;

                // إزالة الرحلة من ActiveTripStore
                _activeTripStore.RemoveDriverTrip(trip.DriverId);

                foreach (var o in trip.TripOrders.Where(x => x.StatusInTrip == TripOrderStatus.DroppedOff))
                {
                    await _notification.SendNotificationAsync(
     o.Order.PassengerId,
     NotificationType.RateTrip,
     "Rate your trip",
     "Please rate your driver",
     o.OrderId,
     trip.TripId,
     extraData: new
     {
         orderId = o.OrderId,
         tripId = trip.TripId,
         countdown = 1800 // 30 دقيقة
     }
 );
                    // ✅ إزالة الركاب من الـgroup الخاص بالرحلة
                    await _hubContext.Clients.Group($"user-{o.Order.PassengerId}")
                                            .SendAsync("LeaveTrip", trip.TripId);
                }
            }
            else
            {
                // 3. تحديث حالة السائق بناءً على الركاب المتبقين (للسماح بركاب جدد في الرحلات المشتركة)
                int remainingPassengers = trip.TripOrders
                    .Where(o => o.StatusInTrip == TripOrderStatus.PickedUp)
                    .Sum(o => o.Order.PassengerCount);

                driver.Status = remainingPassengers < vehicle.Seats ? DriverStatus.Shared : DriverStatus.busy;

                // إشعار الراكب الذي نزل فقط
                await _notification.SendNotificationAsync(
                    tripOrder.Order.PassengerId,
                    NotificationType.RateTrip,
                    "Trip Ended",
                    "You have arrived at your destination",
                    orderId,
                    trip.TripId
                );
            }

            await _context.SaveChangesAsync();

            if (!allOrdersFinished && trip.TripOrders.Count > 1)
            {
                await _tripRoutingService.RecalculateTripAsync(trip.TripId);
            }

            return "Success";
        }


        public async Task<string> CancelTripByDriverAsync(int tripId, string driverId, TripCancelReason reason)
{
            // 1️⃣ جلب الرحلة مع كافة تفاصيل الركاب والسائق في استعلام واحد
            var trip = await _context.Trips
                .Include(t => t.TripOrders).ThenInclude(o => o.Order)
                .Include(t => t.Driver).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(t => t.TripId == tripId && t.DriverId == driverId);

            if (trip == null) return "Trip not found or unauthorized";

            if (trip.Status != TripStatus.Assigned && trip.Status != TripStatus.InProgress)
                return "Trip cannot be cancelled in current state";

            // 2️⃣ تحديث حالة السائق وإرسال تحديث فوري له عبر SignalR
            if (trip.Driver != null)
            {
                trip.Driver.Status = (reason == TripCancelReason.Accident)
                    ? DriverStatus.offline
                    : DriverStatus.available;

                // 📡 إخبار تطبيق السائق بتغيير حالته فوراً (لتحديث الواجهة لديه)
                await _hubContext.Clients.Group($"user-{driverId}")
                    .SendAsync("UpdateDriverStatus", new
                    {
                        status = trip.Driver.Status.ToString(),
                        reason = reason.ToString()
                    });
            }

            // 2️⃣ إعادة تصفير الرحلة وتحديث "عداد" الوقت (مهم جداً للـ Background Service)
            trip.Status = TripStatus.SearchingDriver;
            trip.DriverId = null;
            trip.TripOfferSentAt = DateTime.UtcNow; // 🔥 إعادة ضبط المهلة (3 دقائق جديدة)
            trip.LastOfferedDriverId = null;        // 🔥 مسح آخر سائق للسماح بالبحث من جديد
            trip.AssignedAt = null;
            trip.StartTime = null;

            // 3️⃣ إعادة الطلبات لحالة "البحث" وتصفير الرحلة
            foreach (var tripOrder in trip.TripOrders)
            {
                tripOrder.StatusInTrip = TripOrderStatus.Unassigned;
                tripOrder.Order.Status = OrderStatus.SearchingDriver;
                tripOrder.UnassignedAt = DateTime.UtcNow;
            }

          
            _activeTripStore.RemoveDriverTrip(driverId);

            await _context.SaveChangesAsync();

            // 4️⃣ إخطار الركاب وإخراجهم من "مجموعة الرحلة" في SignalR
            foreach (var tripOrder in trip.TripOrders)
            {
                // إشعار (Push Notification)
                await _notification.SendNotificationAsync(
                    tripOrder.Order.PassengerId,
                    NotificationType.DriverCancelledTrip,
                    "تم إلغاء الرحلة",
                    "نعتذر، السائق ألغى الرحلة. جاري البحث عن بديل.",
                    tripOrder.OrderId,
                    trip.TripId,
                    extraData: new
                    {
                        tripId,
                        status = "cancelled",
                        reason = reason.ToString()
                    }
                );

                // أمر لحظي للتطبيق (SignalR) لإغلاق صفحة الرحلة
                await _hubContext.Clients.Group($"user-{tripOrder.Order.PassengerId}")
                    .SendAsync("LeaveTrip", tripId);
            }

            // 5️⃣ إخطار المكتب لتحديث الخريطة لديهم فوراً
            await _notification.SendOfficeNotificationAsync(
    officeUserId: _settings.OfficeUserId,
    type: NotificationType.DriverCancelledTrip,
    title: "Trip Cancelled",
    body: $"Trip {tripId} cancelled by driver {driverId}",
    orderId: null,
    tripId: tripId,
    extraData: new
    {
        driverId,
        reason = reason.ToString()
    },
    saveToDb: true
);

            if (trip.IsManuallyAssigned)
            {
                // رجّعها Auto
                trip.IsManuallyAssigned = false;

                await AssignTripEmergencyAsync(tripId, driverId);
            }
            else
            {
                await AssignTripEmergencyAsync(tripId, driverId);
            }

            return "Trip cancelled successfully";

        }

       


        private async Task<double> GetDriverWeightedRating(string driverId)
        {
            double minVotes = 20;

            var stats = await _context.Ratings
                .Where(r => r.TargetUserId == driverId)
                .GroupBy(r => r.TargetUserId)
                .Select(g => new
                {
                    Count = g.Count(),
                    Avg = g.Average(x => x.Stars)
                })
                .FirstOrDefaultAsync();

            if (stats == null)
                return 5;

            var globalAvg = await _context.Ratings
                .AverageAsync(r => (double?)r.Stars) ?? 5;

            double weighted =
                ((stats.Avg * stats.Count) + (globalAvg * minVotes)) /
                (stats.Count + minVotes);

            return Math.Round(weighted, 2);
        }

        private async Task RemoveDriverFromQueue(string driverId)
        {
            var entries = await _context.OfficeQueueEntries
                .Where(q => q.DriverId == driverId && q.Status == QueueStatus.InQueue)
                .ToListAsync();

            if (!entries.Any()) return;

            var now = DateTime.UtcNow;

            foreach (var entry in entries)
            {
                entry.Status = QueueStatus.LeftQueue;
                entry.LeftAt = DateTime.UtcNow;

              
            }

            await _context.SaveChangesAsync();

            await _notification.SendOfficeNotificationAsync(
      officeUserId: _settings.OfficeUserId,
      type: NotificationType.DriverLeftQueue,
      title: "Driver Left Queue",
      body: $"Driver {driverId} left the queue",
      orderId: null,
      tripId: null,
      extraData: new
      {
          driverId,
          action = "left",
          time = now
      },
      saveToDb: true
  );


        }



    }
}
