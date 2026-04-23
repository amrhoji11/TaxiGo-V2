using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class DriverTripReportDto
    {
        public string PassengerName { get; set; }
        public string PickupLocation { get; set; }
        public string Destination { get; set; }
        public TimeSpan Duration { get; set; }
        public int? Rating { get; set; }
        public string Comment { get; set; }
        public DateTime CompletedAt { get; set; }
    }
}
