using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Infrastructure.Helper
{
    public class GoogleMapService : IMapService
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        public GoogleMapService(HttpClient httpClient, IConfiguration configuration)
        {
            _apiKey = configuration["GoogleMaps:ApiKey"];
            _httpClient = httpClient;

        }

        // Single ETA
        // ===============================
        public async Task<TimeSpan> GetETAAsync(
            decimal originLat,
            decimal originLng,
            decimal destLat,
            decimal destLng)
        {
            try
            {
                string url =
                    $"https://maps.googleapis.com/maps/api/distancematrix/json" +
                    $"?origins={originLat},{originLng}" +
                    $"&destinations={destLat},{destLng}" +
                    $"&mode=driving&key={_apiKey}";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return TimeSpan.MaxValue;

                var json = await response.Content.ReadAsStringAsync();

                using JsonDocument doc = JsonDocument.Parse(json);

                var rows = doc.RootElement.GetProperty("rows");

                if (rows.GetArrayLength() == 0)
                    return TimeSpan.MaxValue;

                var element = rows[0].GetProperty("elements")[0];

                if (element.GetProperty("status").GetString() != "OK")
                    return TimeSpan.MaxValue;

                var seconds = element
                    .GetProperty("duration")
                    .GetProperty("value")
                    .GetDouble();

                return TimeSpan.FromSeconds(seconds);
            }
            catch
            {
                return TimeSpan.MaxValue;
            }
        }

        // ===============================
        // Matrix ETA for many drivers
        // ===============================
        public async Task<List<TimeSpan>> GetDistancesAsync(
            List<DriverLocationDto> drivers,
            double destLat,
            double destLng)
        {
            try
            {
                var origins = string.Join("|",
                    drivers.Select(d => $"{d.Lat},{d.Lng}"));

                string url =
                    $"https://maps.googleapis.com/maps/api/distancematrix/json" +
                    $"?origins={origins}" +
                    $"&destinations={destLat},{destLng}" +
                    $"&mode=driving&key={_apiKey}";

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    return drivers.Select(_ => TimeSpan.MaxValue).ToList();

                var json = await response.Content.ReadAsStringAsync();

                using JsonDocument doc = JsonDocument.Parse(json);

                var rows = doc.RootElement.GetProperty("rows");

                var result = new List<TimeSpan>();

                foreach (var row in rows.EnumerateArray())
                {
                    var element = row.GetProperty("elements")[0];

                    if (element.GetProperty("status").GetString() != "OK")
                    {
                        result.Add(TimeSpan.MaxValue);
                        continue;
                    }

                    var seconds = element
                        .GetProperty("duration")
                        .GetProperty("value")
                        .GetDouble();

                    result.Add(TimeSpan.FromSeconds(seconds));
                }

                return result;
            }
            catch
            {
                return drivers.Select(_ => TimeSpan.MaxValue).ToList();
            }
        }

        // ===============================
        // ETA for shared trip
        // ===============================
        public async Task<TimeSpan> GetAdditionalETAToTripAsync(
            Trip trip,
            Order newOrder)
        {
            if (!trip.TripOrders.Any())
                return TimeSpan.Zero;

            var lastOrder = trip.TripOrders.Last().Order;

            decimal startLat =
                lastOrder.DropoffLat ?? lastOrder.PickupLat;

            decimal startLng =
                lastOrder.DropoffLng ?? lastOrder.PickupLng;

            return await GetETAAsync(
                startLat,
                startLng,
                newOrder.PickupLat,
                newOrder.PickupLng);
        }



        public async Task<string> GetRoutePolylineAsync(List<(double lat, double lng)> points)
        {
            if (points == null || points.Count < 2)
                return "";

            var origin = $"{points.First().lat},{points.First().lng}";
            var destination = $"{points.Last().lat},{points.Last().lng}";

            var waypoints = string.Join("|",
                points.Skip(1).SkipLast(1)
                      .Select(p => $"{p.lat},{p.lng}"));

            string url =
                $"https://maps.googleapis.com/maps/api/directions/json" +
                $"?origin={origin}" +
                $"&destination={destination}" +
                $"&waypoints={waypoints}" +
                $"&key={_apiKey}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return "";

            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);

            var routes = doc.RootElement.GetProperty("routes");

            if (routes.GetArrayLength() == 0)
                return "";

            return routes[0]
                .GetProperty("overview_polyline")
                .GetProperty("points")
                .GetString();
        }
    }
}



