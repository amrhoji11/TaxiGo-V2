using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class AllBlocksDto
    {
        public string UserId { get; set; }

        public string FirstName { get; set; }
        public string  LastName { get; set; }
        [MaxLength(10)]
        [MinLength(10)]
        public string PhoneNumber { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }
        public DateTime? StartsAt { get; set; }


        public DateTime? EndsAt { get; set; }
    }
}
