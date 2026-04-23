using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.Interfaces
{
    public interface IAdminAssignmentRepository
    {
        Task<string> ManualAssignDriverAsync(int orderId, string driverId);
        Task<string> ManualAssignTripAsync(int tripId, string driverId);
        Task<SystemMode> GetModeAsync();
        Task<string> SetModeAsync(SystemMode mode);
    }
}
