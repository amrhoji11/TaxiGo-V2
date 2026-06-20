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

namespace TaxiApp.Backend.Infrastructure.Repositories
{
    public class OrderRepository:Repository<Order>,IOrderRepository
    {
        private readonly ApplicationDbContext context;

        public OrderRepository(ApplicationDbContext context):base(context) 
        {
            this.context = context;
        }

        public async Task<string> CancelOrder(string userId,int id)
        {
            var order = await context.Orders.FirstOrDefaultAsync(a=>a.PassengerId==userId && a.OrderId==id);
            if (order == null)
            {
                return "لا يوجد طلب ";
            }

            if (order.Status== OrderStatus.AssignedToTrip || order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled)
            {
                return "لا يمكن الغاء الطلب" ;
            }

            order.Status = OrderStatus.Cancelled;
            order.CancelledAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();

            return "تم الالغاء بنجاح";


        }

        public async Task<Order> CreateOrder(string userId,CreateOrderDto dto)
        {
            var order = new Order
            {
                PassengerId = userId,
                PickupLat = dto.PickupLat,
                PickupLng = dto.PickupLng,
                PickupLocation = dto.PickupLocation,
                DropoffLat = dto.DropoffLat,
                DropoffLng = dto.DropoffLng,
                DropoffLocation = dto.DropoffLocation,
                PassengerCount = dto.PassengerCount,
                RequiredVehicleSize = dto.RequiredVehicleSize,
                OrderTime = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                Status = OrderStatus.Pending,
                NeedsOfficeReview = dto.RequiredVehicleSize == Enums.Large


            };
             await context.AddAsync(order);
            await context.SaveChangesAsync();

            return order;

        }

        public async Task<PagedResult<OrderDto>> GetOrdersForPassengerAsync(
            string passengerId, int pageNumber, int pageSize, DateTime? fromDate, DateTime? toDate)
        {
            var query = context.Orders
                .Where(o => o.PassengerId == passengerId &&
                            (!fromDate.HasValue || o.CreatedAt >= fromDate.Value) &&
                            (!toDate.HasValue || o.CreatedAt <= toDate.Value))
                .OrderByDescending(o => o.CreatedAt);

            var totalCount = await query.CountAsync();

            var ratings = context.Ratings.AsQueryable();

            var data = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new OrderDto
                {
                    OrderId = o.OrderId,
                    PickupLat = o.PickupLat,
                    PickupLng = o.PickupLng,
                    PickupLocation = o.PickupLocation,
                    DropoffLat = o.DropoffLat,
                    DropoffLng = o.DropoffLng,
                    DropoffLocation = o.DropoffLocation,
                    PassengerCount = o.PassengerCount,
                    Priority = o.Priority,
                    RequiredVehicleSize = o.RequiredVehicleSize,
                    Status = o.Status,
                    TripId = o.TripOrders
                        .Where(t => t.StatusInTrip != TripOrderStatus.Unassigned)
                        .OrderByDescending(t => t.AssignedAt)
                        .Select(t => t.TripId)
                        .FirstOrDefault(),
                    Rating = ratings
                        .Where(r => r.OrderId == o.OrderId)
                        .Select(r => new OrderRatingDto { Stars = (int?)r.Stars, Comment = r.Comment })
                        .FirstOrDefault(),
                    CreatedAt = o.CreatedAt
                })
                .ToListAsync();

            return new PagedResult<OrderDto>
            {
                TotalCount = totalCount,
                Page = pageNumber,
                PageSize = pageSize,
                Data = data
            };
        }

        public async Task<OrderDetailDto?> GetOrderDetailAsync(string passengerId, int orderId)
        {
            var order = await context.Orders
                .Include(o => o.TripOrders)
                    .ThenInclude(to => to.Trip)
                        .ThenInclude(t => t.Driver)
                            .ThenInclude(d => d.User)
                .Include(o => o.TripOrders)
                    .ThenInclude(to => to.Trip)
                        .ThenInclude(t => t.Driver)
                            .ThenInclude(d => d.Vehicles)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.PassengerId == passengerId);

            if (order == null)
                return null;

            var activeTripOrder = order.TripOrders
                .Where(to => to.StatusInTrip != TripOrderStatus.Unassigned)
                .OrderByDescending(to => to.AssignedAt)
                .FirstOrDefault();

            var trip = activeTripOrder?.Trip;
            var driver = trip?.Driver;
            var vehicle = driver?.Vehicles.FirstOrDefault(v => v.IsCurrent);

            var rating = await context.Ratings
                .Where(r => r.OrderId == orderId)
                .Select(r => new OrderRatingDto { Stars = (int?)r.Stars, Comment = r.Comment })
                .FirstOrDefaultAsync();

            return new OrderDetailDto
            {
                OrderId = order.OrderId,
                PickupLat = order.PickupLat,
                PickupLng = order.PickupLng,
                PickupLocation = order.PickupLocation,
                DropoffLat = order.DropoffLat,
                DropoffLng = order.DropoffLng,
                DropoffLocation = order.DropoffLocation,
                PassengerCount = order.PassengerCount,
                Priority = order.Priority,
                RequiredVehicleSize = order.RequiredVehicleSize,
                Status = order.Status,
                CreatedAt = order.CreatedAt,
                Rating = rating,

                TripId = trip?.TripId,
                TripStatus = trip?.Status,
                DriverId = driver?.UserId,
                DriverName = driver?.User != null ? $"{driver.User.FirstName} {driver.User.LastName}" : null,
                DriverProfilePhotoUrl = driver?.ProfilePhotoUrl,
                DriverLastLat = driver?.LastLat,
                DriverLastLng = driver?.LastLng,
                VehiclePlateNumber = vehicle?.PlateNumber,
                VehicleMake = vehicle?.Make,
                VehicleModel = vehicle?.Model,
                VehicleColor = vehicle?.Color,
                VehicleSeats = vehicle?.Seats
            };
        }

        public async Task<string> EditOrder(string userId,int id ,EditOrderDto dto)
        {
            var OrderInDb = await context.Orders.FirstOrDefaultAsync(a=>a.PassengerId==userId && a.OrderId==id);
            if (OrderInDb == null)
            {
                return "لا يوجد طلب ";
            }
            if (OrderInDb.Status == OrderStatus.AssignedToTrip || OrderInDb.Status == OrderStatus.Completed || OrderInDb.Status == OrderStatus.Cancelled)
            {
                return "لا يمكن تعديل الطلب ";
            }


            OrderInDb.PickupLat = dto.PickupLat;
            OrderInDb.PickupLng = dto.PickupLng;
            OrderInDb.PickupLocation = dto.PickupLocation;

            OrderInDb.DropoffLat = dto.DropoffLat;
            OrderInDb.DropoffLng = dto.DropoffLng;
            OrderInDb.DropoffLocation = dto.DropoffLocation;

            OrderInDb.Priority = dto.Priority;
            OrderInDb.RequiredVehicleSize = dto.RequiredVehicleSize;
            OrderInDb.PassengerCount = dto.PassengerCount;

            OrderInDb.UpdatedAt = DateTime.UtcNow;

           await context.SaveChangesAsync();

            return "تم تعديل الطلب بنجاح";



        }

       
    }
}
