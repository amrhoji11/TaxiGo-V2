using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.Interfaces
{
    public interface IDriverAssignmentRepository
    {
        Task<string> DriverAssignAsync(Order order,  string? excludedDriverId = null);

        Task<string> DriverAcceptOrderAsync(int orderId, string driverId);

        // تظهر في حالة ان هناك سائق الغى رحلته ويتم البحث عن سائق مناسب لاخذ هذه الرحلة بكل ما فيها 
        Task<string> DriverAcceptTripAsync(int tripId, string driverId);

        Task<string> DriverRejectTripAsync(int tripId, string driverId);

        Task<string> DriverRejectOrderAsync(int orderId, string driverId);

        Task<string> EnterQueueAsync(string driverId);

        Task<string> DriverArrivedAsync(int orderId, string driverId);
        Task<string> StartTripAsync(int tripId,string driverId);
        Task<string> DropoffAsync(string driverId, int orderId);
        Task<string> PickupAsync(string driverId, int orderId);

        Task<string> CancelTripByDriverAsync(int tripId, string driverId, TripCancelReason reason);

        Task<string> AssignTripEmergencyAsync(int tripId, string? excludedDriverId = null);

       

    }
}