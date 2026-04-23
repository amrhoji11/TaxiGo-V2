using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class PassengerTripReportDto
    {
        public string DriverName { get; set; } = default!;
        public string PickupLocation { get; set; } = default!;
        public string Destination { get; set; } = default!;
        public TimeSpan Duration { get; set; }
        public int? Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CompletedAt { get; set; }
    }
}
