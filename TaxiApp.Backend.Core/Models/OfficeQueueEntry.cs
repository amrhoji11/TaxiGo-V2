using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.Models
{
    public enum QueueStatus
    {
        InQueue = 0,
        LeftQueue = 1
    }

    public class OfficeQueueEntry
    {
        public int QueueEntryId { get; set; }

        [ForeignKey(nameof(Driver))]
        [Required]
        public string DriverId { get; set; }
        public DateTime EnteredAt { get; set; }= DateTime.UtcNow;
        public DateTime? LeftAt { get; set; }

        public QueueStatus Status { get; set; }

        // Navigation
        public Driver Driver { get; set; }
    }
}
