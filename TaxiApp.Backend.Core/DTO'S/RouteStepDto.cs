using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class RouteStepDto
    {
        public int OrderId { get; set; }

        public double Lat { get; set; }
        public double Lng { get; set; }

        public bool IsPickup { get; set; } // true = pickup, false = dropoff

        public int Sequence { get; set; } // ترتيب النقطة في المسار

        public int EstimatedMinutes { get; set; } // الوقت للوصول لهذه النقطة

        public string Label { get; set; } // نص يظهر في الخريطة

        public string PassengerName { get; set; }

        public string PassengerId { get; set; }
    }
}
