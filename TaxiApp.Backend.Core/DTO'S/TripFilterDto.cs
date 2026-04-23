using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class TripFilterDto
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 10;

        public TripStatus? Status { get; set; }
        public string? Search { get; set; }
        public TripSortBy? SortBy { get; set; }
        public bool? Ascending { get; set; } = true;
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
}
