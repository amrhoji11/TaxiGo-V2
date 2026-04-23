using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class DriverPendingResponseListDto
    {
        public int Count { get; set; }
        public IEnumerable<DriverPendingResponseDto> Drivers { get; set; }
    }
}
