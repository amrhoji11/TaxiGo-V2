using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class VehiclesResponseDto
    {
        [Key]
        public int VehicleId { get; set; }

        [Required]
        [ForeignKey(nameof(Driver))]
        public string? DriverId { get; set; }


        public string? PlatePhotoUrl { get; set; }
        [Required]
        [MaxLength(20)]
        public string PlateNumber { get; set; }

        public Enums VehicleSize { get; set; }//حجم السيارة
        public int Seats { get; set; }//عدد المقاعد المتوفرةللركاب

        public string Make { get; set; }//الشركة المصنعة مثل Kia
        public string Model { get; set; }
        public string Color { get; set; }
        public int? Year { get; set; }

        public bool IsActive { get; set; }
        public bool IsCurrent { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

    }
}
