using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.Interfaces
{
    public interface IDriverTrackingRepository
    {
        Task UpdateDriverLocationAsync(
        string driverId,
        decimal lat,
        decimal lng);
    }
}
