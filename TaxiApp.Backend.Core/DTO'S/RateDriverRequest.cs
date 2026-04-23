using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class RateDriverRequest
    {
        public int OrderId { get; set; }
        public int Stars { get; set; }
        public string? Comment { get; set; }

    }
}
