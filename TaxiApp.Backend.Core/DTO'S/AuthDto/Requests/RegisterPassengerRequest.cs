using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.DTO_S.AuthDto
{
    public class RegisterPassengerRequest
    {
        [MinLength(3)]
        public string FirstName { get; set; }
        [MinLength(3)]
        public string LastName { get; set; }

        [Required]
        public string CountryCode { get; set; } // +970 / +972

        [Required]
        [RegularExpression(@"^\d{9,10}$", ErrorMessage = "رقم الهاتف غير صحيح")]
        public string PhoneNumber { get; set; }

        public string? ProfilePhotoUrl { get; set; }



    }
}
