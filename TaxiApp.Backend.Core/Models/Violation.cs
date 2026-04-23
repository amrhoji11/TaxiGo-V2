using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.Models
{
    public enum ViolationType
    {
        Behavior = 0,
        Delay = 1,
        Cancellation = 2
    }

    public enum ViolationStatus
    {
        Active = 0,
        Resolved = 1
    }
    public class Violation
    {
        public int Id { get; set; }

        public string DriverId { get; set; }

        public int? OrderId { get; set; }
        public int? TripId { get; set; }

        public ViolationType Type { get; set; }

        public ViolationStatus Status { get; set; } = ViolationStatus.Active;


        public string Reason { get; set; }

        public DateTime? ResolvedAt  { get; set; } 
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
