using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.Interfaces
{
    public interface IAdminRepository
    {
        Task<bool> UpdateAdminProfileAsync(string userId, UpdateAdminProfileDto request);
        Task<ApplicationUser?> GetAdminProfileAsync(string adminId);

        Task<bool> SoftDeleteDriverAsync(string driverId);
        Task<List<Driver>> GetActiveDriversAsync();
        Task RestoreDriverAsync(string driverId);
        Task<bool> SoftDeletePassengerAsync(string passengerId);
        Task RestorePassengerAsync(string passengerId);
        Task<List<PassengerDto>> GetActivePassengersAsync();

        Task<PassengerProfileDto> GetPassengerProfileAsync(string passengerId);

        Task<PagedResult<OrderDto>> GetOrdersAsync(
    int page,
   int pageSize,
   OrderStatus? status,
   string? search,
   OrderSortBy? sortBy,
   bool? ascending,
   DateTime? fromDate,
   DateTime? toDate);


        Task<PagedResult<TripDto>> GetTripsAsync(
    int page,
    int pageSize,
    TripStatus? status,
    string? search,
    TripSortBy? sortBy,
    bool? ascending,
    DateTime? fromDate,
    DateTime? toDate);


        Task<List<TopDriverDto>> GetTopDriversAsync(int top, DateTime? fromDate, DateTime? toDate);
    }
}
