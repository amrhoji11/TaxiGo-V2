using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.DTO_S.AuthDto.Requests
{
    public class UpdateDriverRequest
    {

        [MinLength(3)]
        public string? FirstName { get; set; }

        [MinLength(3)]
        public string? LastName { get; set; }

        

        public string? Address { get; set; }
        public IFormFile? ProfilePhotoImg { get; set; }
        // حذف الصورة
        public bool RemoveProfilePhoto { get; set; } = false;

        // حذف العنوان
        public bool RemoveAddress { get; set; } = false;
    }
}
