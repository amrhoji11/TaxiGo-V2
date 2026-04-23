using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class TopDriverDto
    {
        public string DriverId { get; set; }
        public string DriverName { get; set; }
        public int CompletedTrips { get; set; }
        public double AvgRating { get; set; }
        public int ViolationsCount { get; set; }
        public double Score { get; set; }
    }
}
