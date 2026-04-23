using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class TripDto
    {
        public int TripId { get; set; }
    public TripStatus Status { get; set; }
    public string DriverName { get; set; }
    public int TotalPassengers { get; set; }
    public int RatingCount { get; set; }
    public double TripRating { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    }
}
