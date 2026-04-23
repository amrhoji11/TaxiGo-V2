using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class ResponseCreateOrderDto
    {
        [Required]
        public decimal PickupLat { get; set; }

        [Required]
        public decimal PickupLng { get; set; }

        [Required]
        public string PickupLocation { get; set; }

        // Dropoff (اختياري)
        public decimal? DropoffLat { get; set; }
        public decimal? DropoffLng { get; set; }
        public string? DropoffLocation { get; set; }

        // Order preferences
        public OrderPriority Priority { get; set; } = OrderPriority.Normal;

        public Enums? RequiredVehicleSize { get; set; }

        [Range(1, 10)]
        public int PassengerCount { get; set; }
    }
}
