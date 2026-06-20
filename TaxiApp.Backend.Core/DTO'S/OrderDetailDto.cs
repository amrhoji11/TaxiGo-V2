using System;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class OrderDetailDto
    {
        public int OrderId { get; set; }
        public decimal PickupLat { get; set; }
        public decimal PickupLng { get; set; }
        public string PickupLocation { get; set; }
        public decimal? DropoffLat { get; set; }
        public decimal? DropoffLng { get; set; }
        public string? DropoffLocation { get; set; }
        public int PassengerCount { get; set; }
        public OrderPriority Priority { get; set; }
        public Enums? RequiredVehicleSize { get; set; }
        public OrderStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public OrderRatingDto? Rating { get; set; }

        // Populated once a driver has been assigned (null/unset before that).
        public int? TripId { get; set; }
        public TripStatus? TripStatus { get; set; }
        public string? DriverId { get; set; }
        public string? DriverName { get; set; }
        public string? DriverProfilePhotoUrl { get; set; }
        public decimal? DriverLastLat { get; set; }
        public decimal? DriverLastLng { get; set; }
        public string? VehiclePlateNumber { get; set; }
        public string? VehicleMake { get; set; }
        public string? VehicleModel { get; set; }
        public string? VehicleColor { get; set; }
        public int? VehicleSeats { get; set; }
    }
}
