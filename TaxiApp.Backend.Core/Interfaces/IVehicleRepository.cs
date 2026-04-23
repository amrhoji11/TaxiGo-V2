using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.DTO_S;

using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.Interfaces
{
    public interface IVehicleRepository:IRepository<Vehicle>
    {
        Task<Vehicle> AddVehicel(AddVehicleDto dto);
        Task<IEnumerable<Vehicle>> GetUnassignedAsync(int pageNumber = 1, int pageSize = 10);
        Task<bool> Unassigned(int vehicleId);
        Task<bool> ToggleActive(int vehicleId);
        Task<bool> EditVehicle(int id,EditVehicleDto dto);
        Task<bool> AssignVehicleToDriver(int vehicleId , string driverId);
    }
}
