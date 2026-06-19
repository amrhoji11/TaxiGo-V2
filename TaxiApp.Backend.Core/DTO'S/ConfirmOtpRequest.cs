using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class ConfirmOtpRequest
    {
        public string CountryCode { get; set; }
        public string PhoneNumber { get; set; }
        public string Otp { get; set; }
    }
}
