using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.Interfaces
{
    public interface IMapService
    {
        Task<TimeSpan> GetETAAsync(
          decimal originLat,
          decimal originLng,
          decimal destLat,
          decimal destLng);

        Task<List<TimeSpan>> GetDistancesAsync(
            List<DriverLocationDto> drivers,
            double destLat,
            double destLng);

        Task<TimeSpan> GetAdditionalETAToTripAsync(
            Trip trip,
            Order newOrder);

        Task<string> GetRoutePolylineAsync(List<(double lat, double lng)> points);
    }
}
