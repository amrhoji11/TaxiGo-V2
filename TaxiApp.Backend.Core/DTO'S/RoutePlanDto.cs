using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class RoutePlanDto
    {
        public List<RouteStepDto> Steps { get; set; } = new();
        public string Polyline { get; set; }
        public int TotalMinutes { get; set; }
    }
}
