using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Core.Models;
using TaxiApp.Backend.Core.Settings;

namespace TaxiApp.Backend.Infrastructure.Repositories
{
    public class OrderService
    {
        private readonly IOrderRepository _orderRepo;
        private readonly IDriverAssignmentRepository _driverRepo;
        private readonly IMapService _mapService;
        private readonly INotificationRepository notificationRepository;
        private readonly TaxiSettings _settings;

        public OrderService(
    IOrderRepository orderRepo,
    IDriverAssignmentRepository driverRepo,
    IMapService mapService,INotificationRepository notificationRepository, IOptions<TaxiSettings> settings)
        {
            _orderRepo = orderRepo;
            _driverRepo = driverRepo;
            _mapService = mapService;
            this.notificationRepository = notificationRepository;
            _settings = settings.Value;
        }

        public async Task<Order> CreateAndAssign(string userId, CreateOrderDto dto)
        {
            var order = await _orderRepo.CreateOrder(userId, dto);

            // 3. إرسال إشعار فوري للراكب يحتوي على الإحداثيات لرسم العلامة
            await notificationRepository.SendNotificationAsync(
                userId: userId,
                type: NotificationType.OrderCreated, // تأكد أن هذا النوع موجود في الـ Enum عندك
                title: "تم استلام طلبك",
                body: "نبحث لك عن أقرب سائق الآن...",
                orderId: order.OrderId,
                extraData: new
                {
                    pickupLat = order.PickupLat,
                    pickupLng = order.PickupLng
                }
            );
            if (_settings.EnableAutoAssignment)
            {
                await _driverRepo.DriverAssignAsync(order);
            }

            

            return order;
        }
    }
}
