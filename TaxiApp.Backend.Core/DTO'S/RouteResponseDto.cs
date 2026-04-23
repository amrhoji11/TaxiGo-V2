using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class RouteResponseDto
    {
        public List<RouteStepDto> Route { get; set; }
        public string Polyline { get; set; }
        public int TotalMinutes { get; set; }

        public IEnumerable<RouteStepDto> Pickups { get; set; }
        public IEnumerable<RouteStepDto> Dropoffs { get; set; }
    }
}
