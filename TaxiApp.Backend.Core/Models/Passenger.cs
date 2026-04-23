using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.Models
{
    public class Passenger
    {
        [Key]
        public string UserId { get; set; }
        public string?  ProfilePhotoUrl { get; set; }

        [NotMapped]
        public string Address => User?.Address;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public ApplicationUser User { get; set; }
        public ICollection<Order> Orders { get; set; } = new List<Order>();

    }
}
