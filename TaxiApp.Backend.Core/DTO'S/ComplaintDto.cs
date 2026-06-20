using System;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class ComplaintDto
    {
        public int Id { get; set; }
        public string SenderId { get; set; }
        public string SenderName { get; set; }
        public string? AgainstUserId { get; set; }
        public string? AgainstUserName { get; set; }
        public int? OrderId { get; set; }
        public int? TripId { get; set; }
        public ComplaintReason ReasonType { get; set; }
        public ComplaintTargetType TargetType { get; set; }
        public string Description { get; set; }
        public ComplaintStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public int? ViolationId { get; set; }
        public ViolationDto? Violation { get; set; }
    }
}
