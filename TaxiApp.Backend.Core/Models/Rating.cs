using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.Models
{
    public class Rating
    {
        [Key]
        public int RatingId { get; set; }

        // Foreign Key: يربط التقييم بالرحلة التي حصل فيها التقييم
        public int TripId { get; set; }

        public int OrderId { get; set; }


        // Foreign Key: الشخص الذي كتب التقييم (الذي قيّم)
        // مثال: Passenger يقيّم Driver
        [Required]
        public string RaterUserId { get; set; }

        // Foreign Key: الشخص الذي تم تقييمه
        // مثال: Driver الذي تم تقييمه من Passenger
        [Required]
        public string TargetUserId { get; set; }

        [Range(1,5)]
        public int Stars { get; set; }

        [MaxLength(500)]
        public string? Comment { get; set; }

        // وقت إنشاء التقييم
        public DateTime RatedAt { get; set; } = DateTime.UtcNow;

        // ---------------- Navigation Properties ----------------

        // Navigation: الوصول إلى بيانات الرحلة المرتبط بها التقييم
        public Trip Trip { get; set; }

        // Navigation: المستخدم الذي كتب التقييم
        public ApplicationUser Rater { get; set; }

        // Navigation: المستخدم الذي تم تقييمه
        public ApplicationUser Target { get; set; }

        public Order Order { get; set; }
    }
}
