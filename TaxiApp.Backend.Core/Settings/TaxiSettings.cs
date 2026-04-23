using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.Settings
{
    
    public class TaxiSettings
    {
        public int QueueBaseEtaMinutes { get; set; } = 10;
        public int MaxQueueBoost { get; set; } = 20;
        public double MaxAcceptableScore { get; set; } = 70;
        public int SearchRadiusMeters { get; set; }
        public int MaxSharedEtaMinutes { get; set; }
        public int MaxEtaMinutes { get; set; }
        public int EtaCacheSeconds { get; set; }
        public bool EnableAutoAssignment { get; set; }

       


        public string OfficeUserId { get; set; }
    }
}
