using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class ToggleUserBlockDto
    {
        [MaxLength(500)]
        public string? Reason { get; set; }

        public DateTime? EndsAt { get; set; }
    }
}
