using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class UpdateComplaintStatusDto
    {
        
        public ComplaintStatus Status { get; set; }

        public ViolationType ViolationType { get; set; }

        public bool CreateViolation { get; set; }
        public string? ViolationReason { get; set; }
    }
}
