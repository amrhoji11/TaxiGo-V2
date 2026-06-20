using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class DriverProfileDto
    {
        public string UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? ProfilePhotoUrl { get; set; }
        public DriverStatus Status { get; set; }
        public bool IsInQueue { get; set; }
        public ApprovalStatus ApprovalStatus { get; set; }
        public string? VehiclePlateNumber { get; set; }
        public Enums? VehicleSize { get; set; }
        public int? VehicleSeats { get; set; }
        public string? VehicleMake { get; set; }
        public string? VehicleModel { get; set; }
        public string? VehicleColor { get; set; }
    }
}
