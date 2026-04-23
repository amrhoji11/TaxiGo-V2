using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class UpdateAdminProfileDto
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
        public IFormFile? ProfilePhotoImg { get; set; } // صورة جديدة
        public bool RemoveProfilePhoto { get; set; }   // خيار حذف الصورة
        public bool RemoveAddress { get; set; }        // خيار حذف العنوان
    }
}
