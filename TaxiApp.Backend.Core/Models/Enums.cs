using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.Models
{
    public enum Enums
    {

        Small = 0,    // سيارة صغيرة (سيدان، يارس، أكسنت...)
        Medium = 1,   // سيارة متوسطة (كورولا، إلنترا...)
        Large = 2    // سيارة كبيرة (فان، SUV، عائلي)
    }
    public enum TripCancelReason
    {
        DriverIssue = 0,
        VehicleProblem = 1,
        Accident = 2,
        Emergency = 3
    }

    public enum OrderSortBy
    {
        CreatedAt = 0,
        PassengerName = 1,
        Status = 2
    }


    public enum TripSortBy
    {
        CreatedAt = 0,
        DriverName = 1,
        Status = 2
    }
}
