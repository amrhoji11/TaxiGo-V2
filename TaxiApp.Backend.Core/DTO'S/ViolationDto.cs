using System;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class ViolationDto
    {
        public int Id { get; set; }
        public string DriverId { get; set; }
        public string DriverName { get; set; }
        public int? OrderId { get; set; }
        public int? TripId { get; set; }
        public ViolationType Type { get; set; }
        public ViolationStatus Status { get; set; }
        public string Reason { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
