using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.Interfaces
{
    public interface IOrderRepository: IRepository<Order>
    {
        Task<Order> CreateOrder(string userId,CreateOrderDto dto);
        Task<string> EditOrder( string userId,int id,EditOrderDto dto);
        Task<string> CancelOrder(string userId,int id);
    }
}
