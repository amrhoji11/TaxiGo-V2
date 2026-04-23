using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Infrastructure.Helper
{
    public class ActiveTripStore
    {
        private readonly ConcurrentDictionary<string, int> _driverTrips = new();

        public void SetDriverTrip(string driverId, int tripId)
        {
            _driverTrips[driverId] = tripId;
        }

        public void RemoveDriverTrip(string driverId)
        {
            _driverTrips.TryRemove(driverId, out _);
        }

        public bool TryGetTrip(string driverId, out int tripId)
        {
            return _driverTrips.TryGetValue(driverId, out tripId);
        }
    }
}
