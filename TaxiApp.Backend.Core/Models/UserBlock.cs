using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.Models
{
    public class UserBlock
    {
        [Key]
        public int BlockId { get; set; }

        public string UserId { get; set; }
        public string? BlockedByUserId { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }

        public DateTime StartsAt { get; set; }
        public DateTime? EndsAt { get; set; }

        public DateTime CreatedAt { get; set; }= DateTime.UtcNow;

        // Navigation
        public ApplicationUser User { get; set; }
        public ApplicationUser? BlockedBy { get; set; }
    }
}
