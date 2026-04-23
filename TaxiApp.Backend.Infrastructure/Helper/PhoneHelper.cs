using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Infrastructure.Helper
{
    public class PhoneHelper
    {
        public static string BuildInternationalPhone(string countryCode, string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return phone;

            phone = phone.Trim();

            if (phone.StartsWith("0"))
                phone = phone.Substring(1);

            return $"{countryCode}{phone}";
        }
    }
}
