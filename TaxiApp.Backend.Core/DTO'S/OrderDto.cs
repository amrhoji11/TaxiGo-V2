using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class OrderDto
    {
        public int OrderId { get; set; }
        public string PassengerName { get; set; }
        public string PickupLocation { get; set; }
        public string DropoffLocation { get; set; }
        public int PassengerCount { get; set; }
        public OrderStatus Status { get; set; }
        public int TripId { get; set; }
        public OrderRatingDto? Rating { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
