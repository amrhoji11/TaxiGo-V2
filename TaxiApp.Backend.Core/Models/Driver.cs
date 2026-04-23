using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.Models
{
    public enum DriverStatus
    {
        
        available = 0,
        busy = 1,
        Shared = 2,
        offline = 3,
        rejected=4
        
    }
    public class Driver
    {
        [Key]
        public string UserId { get; set; }

        [NotMapped]
        public string Address => User?.Address;
        public string? ProfilePhotoUrl { get; set; }

        public DriverStatus Status { get; set; }

        public decimal? LastLat { get; set; }
        public decimal? LastLng { get; set; }


        public DateTime? LastSeenAt { get; set; }


        public DateTime? UpdatedAt { get; set; }

        public bool IsDeleted { get; set; }

        // Navigation
        public ApplicationUser User { get; set; }

        public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
        public ICollection<Trip> Trips { get; set; }= new List<Trip>();
        public ICollection<DriverLocation> Locations { get; set; } = new List<DriverLocation>();

    }
}
