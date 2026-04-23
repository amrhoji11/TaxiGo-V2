using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.Models
{
    public class DriverLocation
    {
        [Key]
        public int LocationId { get; set; }

        // Foreign Key: السائق الذي تم تسجيل موقعه
        // غالباً يشير إلى Driver.UserId
        [Required]
        [ForeignKey(nameof(Driver))]
        public string DriverId { get; set; }

        // Foreign Key (اختياري): إذا كان تسجيل الموقع مرتبط برحلة معينة
        // Nullable لأنه ممكن يكون السائق غير داخل رحلة وقت تسجيل الموقع
        [ForeignKey(nameof(Trip))]
        public int? TripId { get; set; }

        // Latitude: خط العرض (موقع السائق)
        public decimal Lat { get; set; }

        // Longitude: خط الطول (موقع السائق)
        public decimal Lng { get; set; }

        // وقت تسجيل هذا الموقع
        // يفضل يكون UTC
        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

        // ---------------- Navigation Properties ----------------

        // Navigation: الوصول لبيانات السائق
        // العلاقة: Driver (1) -> DriverLocations (Many)
        public Driver Driver { get; set; } 

        // Navigation: الوصول لبيانات الرحلة (إن وجدت)
        // العلاقة: Trip (1) -> DriverLocations (Many) أو قد تكون null
        public Trip? Trip { get; set; }
    }
}
