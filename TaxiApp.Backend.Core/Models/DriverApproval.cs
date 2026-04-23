using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.Models
{
    public enum ApprovalStatus
    {
        pending = 0,   // السائق سجّل وينتظر موافقة المكتب
        approved = 1,  // تم القبول
        rejected = 2   // تم الرفض
    }
    public class DriverApproval
    {
        [Key]
        public string DriverId { get; set; }

        public ApprovalStatus Status { get; set; }

        public string? ReviewedByUserId { get; set; }
        public DateTime? ReviewedAt { get; set; }
        [MaxLength(500)]
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Driver Driver { get; set; }
        public ApplicationUser ReviewedBy { get; set; }
    }
}
