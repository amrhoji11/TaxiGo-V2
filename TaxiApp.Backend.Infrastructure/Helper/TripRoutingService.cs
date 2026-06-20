using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Core.Models;
using TaxiApp.Backend.Infrastructure.Data;

namespace TaxiApp.Backend.Infrastructure.Helper
{
    public class TripRoutingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMapService _mapService;
        private readonly IHubContext<NotificationHub> _hub;

        public TripRoutingService(
            ApplicationDbContext context,
            IMapService mapService,
            IHubContext<NotificationHub> hub)
        {
            _context = context;
            _mapService = mapService;
            _hub = hub;
        }

        // =========================
        // 🔥 MAIN ENGINE
        // =========================
        public async Task<RoutePlanDto> RecalculateTripAsync(int tripId)
        {
            var trip = await _context.Trips
          .Include(t => t.TripOrders)
              .ThenInclude(o => o.Order)
                  .ThenInclude(o => o.Passenger)
                      .ThenInclude(p => p.User)
          .Include(t => t.Driver)
          .FirstOrDefaultAsync(t => t.TripId == tripId);

            if (trip == null || trip.DriverId == null)
                return new RoutePlanDto();

            var driver = trip.Driver;

            var steps = await BuildSharedTripRoute(trip, driver);

            var points = steps
                .Select(s => (s.Lat, s.Lng))
                .ToList();

            var polyline = await _mapService.GetRoutePolylineAsync(points);

            var result = new RoutePlanDto
            {
                Steps = steps,
                Polyline = polyline,
                TotalMinutes = steps.Sum(x => x.EstimatedMinutes)
            };

            // =========================
            // 🔥 إرسال محسّن للفرونت
            // =========================
            await _hub.Clients
                .Group($"user-{driver.UserId}")
                .SendAsync("RouteUpdated", new RouteResponseDto
                {
                    Route = result.Steps,
                    Polyline = result.Polyline,
                    TotalMinutes = result.TotalMinutes,

                    Pickups = result.Steps.Where(x => x.IsPickup),
                    Dropoffs = result.Steps.Where(x => !x.IsPickup)
                });

            // Passenger-safe subset only — Steps/Pickups/Dropoffs carry
            // every stop's passenger name, which would leak a
            // co-passenger's identity/location on a shared trip. The
            // passenger side only ever needs the line itself and the ETA.
            await _hub.Clients
                .Group($"trip-{tripId}")
                .SendAsync("RouteUpdated", new
                {
                    polyline = result.Polyline,
                    totalMinutes = result.TotalMinutes
                });

            return result;
        }

        // =========================
        // 🧠 SMART ROUTING (your improved logic)
        // =========================
        private async Task<List<RouteStepDto>> BuildSharedTripRoute(Trip trip, Driver driver)
        {
            var tripOrders = trip.TripOrders
           .Where(x => x.StatusInTrip != TripOrderStatus.DroppedOff &&
                       x.StatusInTrip != TripOrderStatus.Cancelled)
           .ToList();

            if (!tripOrders.Any())
                return new List<RouteStepDto>();

            double currentLat = (double)driver.LastLat!;
            double currentLng = (double)driver.LastLng!;

            var steps = new List<RouteStepDto>();
            var ordersById = tripOrders.ToDictionary(x => x.OrderId);

            // Each order contributes its remaining legs *in order*: pickup
            // (only if not already picked up) then dropoff. Tracked as two
            // separate sets rather than deriving "is this a pickup or
            // dropoff candidate" from the order's real `StatusInTrip` —
            // that field never changes mid-loop (this method only plans,
            // it doesn't persist), so a single pass could never place both
            // legs for the same not-yet-picked-up order; it would visit
            // the pickup, immediately drop the order from consideration,
            // and never plan the dropoff leg at all.
            var pendingPickups = tripOrders
                .Where(x => x.StatusInTrip != TripOrderStatus.PickedUp)
                .Select(x => x.OrderId)
                .ToHashSet();
            var pendingDropoffs = tripOrders.Select(x => x.OrderId).ToHashSet();

            int seq = 0;

            while (pendingPickups.Count > 0 || pendingDropoffs.Count > 0)
            {
                RouteStepDto best = null;
                double bestTime = double.MaxValue;

                foreach (var orderId in pendingPickups.Union(pendingDropoffs))
                {
                    // An order's dropoff can never be visited before its
                    // own pickup within this same planning pass.
                    var isPickup = pendingPickups.Contains(orderId);
                    if (!isPickup && !pendingDropoffs.Contains(orderId))
                        continue;

                    var o = ordersById[orderId];

                    var lat = isPickup ? o.Order.PickupLat : o.Order.DropoffLat;
                    var lng = isPickup ? o.Order.PickupLng : o.Order.DropoffLng;

                    var eta = await _mapService.GetETAAsync(
                        (decimal)currentLat,
                        (decimal)currentLng,
                        (decimal)lat!,
                        (decimal)lng!);

                    if (eta.TotalSeconds < bestTime)
                    {
                        bestTime = eta.TotalSeconds;

                        best = new RouteStepDto
                        {
                            OrderId = o.OrderId,
                            Lat = (double)lat!,
                            Lng = (double)lng!,
                            IsPickup = isPickup,
                            Label = isPickup ? o.Order.PickupLocation : o.Order.DropoffLocation,
                            PassengerName = o.Order.Passenger?.User.FirstName + " " + o.Order.Passenger?.User.LastName,
                            PassengerId = o.Order.PassengerId
                        };
                    }
                }

                if (best == null)
                    break;

                seq++;
                best.Sequence = seq;
                best.EstimatedMinutes = (int)(bestTime / 60);

                steps.Add(best);

                currentLat = best.Lat;
                currentLng = best.Lng;

                if (best.IsPickup)
                    pendingPickups.Remove(best.OrderId);
                else
                    pendingDropoffs.Remove(best.OrderId);
            }

            return steps;
        }


        public async Task<int> EstimateRouteMinutes(
    double startLat,
    double startLng,
    List<RouteStepDto> steps)
        {
            if (!steps.Any())
                return 0;

            int total = 0;

            double currentLat = startLat;
            double currentLng = startLng;

            foreach (var step in steps)
            {
                var eta = await _mapService.GetETAAsync(
                    (decimal)currentLat,
                    (decimal)currentLng,
                    (decimal)step.Lat,
                    (decimal)step.Lng);

                total += (int)eta.TotalMinutes;

                currentLat = step.Lat;
                currentLng = step.Lng;
            }

            return total;
        }


        public async Task<int> CalculateInsertionCostAsync(Trip trip, Order newOrder)
        {
            var driver = trip.Driver;

            double startLat = (double)driver.LastLat!;
            double startLng = (double)driver.LastLng!;

            // 1️⃣ المسار الحالي
            var currentSteps = await BuildSharedTripRoute(trip, driver);

            int currentTime = await EstimateRouteMinutes(
                startLat,
                startLng,
                currentSteps);

            // 2️⃣ أضف الطلب الجديد
            var simulatedTripOrders = trip.TripOrders
                .Where(x =>
                    x.StatusInTrip != TripOrderStatus.DroppedOff &&
                    x.StatusInTrip != TripOrderStatus.Cancelled)
                .Select(x => new TripOrder
                {
                    Order = x.Order,
                    StatusInTrip = x.StatusInTrip
                })
                .ToList();

            simulatedTripOrders.Add(new TripOrder
            {
                Order = newOrder,
                StatusInTrip = TripOrderStatus.Assigned
            });

            var fakeTrip = new Trip
            {
                Driver = trip.Driver,
                TripOrders = simulatedTripOrders
            };

            var newSteps = await BuildSharedTripRoute(fakeTrip, driver);

            int newTime = await EstimateRouteMinutes(
                startLat,
                startLng,
                newSteps);

            return newTime - currentTime;
        }
    }
}
