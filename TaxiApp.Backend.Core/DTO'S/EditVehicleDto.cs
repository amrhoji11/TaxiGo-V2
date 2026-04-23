using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class EditVehicleDto
    {
        
        [MaxLength(20)]
        public string? PlateNumber { get; set; }

        public IFormFile? PlatePhotoImg { get; set; }

        public Enums? VehicleSize { get; set; }//حجم السيارة
        public int? Seats { get; set; }//عدد المقاعد المتوفرةللركاب

        public string? Make { get; set; }//الشركة المصنعة مثل Kia
        public string? Model { get; set; }
        public string? Color { get; set; }
        public int? Year { get; set; }
    }
}
