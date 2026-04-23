using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.Models
{

    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }

        public string? FullName => $"{FirstName} {LastName}";

        public string? Address { get; set; }
        public string? ProfilePhotoImg { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? Language { get; set; }
        public bool NotificationsEnabled { get; set; } = true; // ✅ افتراضي شغال
        public bool IsDarkModeEnabled { get; set; } = false;
        public bool IsDeleted { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public Passenger Passenger { get; set; }
        public Driver Driver { get; set; }
        public ICollection<Message> SentMessages { get; set; } = new List<Message>();
        public ICollection<Message> ReceivedMessages { get; set; } = new List<Message>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public ICollection<OrderReview> Reviews { get; set; } = new List<OrderReview>();
        public ICollection<Rating> RatingsGiven { get; set; } = new List<Rating>();
        public ICollection<Rating> RatingsReceived { get; set; } = new List<Rating>();



    }
}