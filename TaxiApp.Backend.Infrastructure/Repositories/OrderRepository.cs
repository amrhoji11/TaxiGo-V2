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
