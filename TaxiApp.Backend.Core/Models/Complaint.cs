using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.Models
{
    public enum ComplaintTargetType
    {
        Driver = 0,
        Passenger = 1,
        Trip = 2
    }

    public enum ComplaintReason
    {
        Behavior,
        Delay,
        Cancellation,
        PaymentIssue,
        RouteIssue,
        Other
    }

    public enum ComplaintStatus
    {
        Pending = 0,
        InReview = 1,
        Resolved = 2,
        Rejected = 3
    }
    public class Complaint
    {
        public int Id { get; set; }

        public string SenderId { get; set; }
        public string? AgainstUserId { get; set; }

        public int? OrderId { get; set; }
        public int? TripId { get; set; }

        public ComplaintReason ReasonType { get; set; }

        public ComplaintTargetType TargetType { get; set; }

        public string Description { get; set; }

        public ComplaintStatus Status { get; set; } = ComplaintStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ResolvedAt { get; set; }

        public int? ViolationId { get; set; }
        public Violation? Violation { get; set; }
    }
}
