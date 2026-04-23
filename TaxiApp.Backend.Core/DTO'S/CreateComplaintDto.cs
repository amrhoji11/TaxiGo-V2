using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.DTO_S
{
    public  class CreateComplaintDto
    {
        public ComplaintTargetType TargetType { get; set; }
        public ComplaintReason ReasonType { get; set; }
        public string Description { get; set; }
    }
}
