using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.Models
{
    public enum ReviewReason
    {
        LargeVehicle = 0,
        HighPassengers = 1,
        TooFar = 2,
        Other = 3
    }

    public enum ReviewStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2
    }
    public class OrderReview
    {
        [Key]
        public int ReviewId { get; set; }

        // Foreign Key: الطلب الذي تمت مراجعته
        [Required]
        public int OrderId { get; set; }

        // نتيجة المراجعة (Approved / Rejected / Pending ... حسب enum ReviewStatus)
        [Required]
        public ReviewStatus Status { get; set; }

        // سبب المراجعة (اختياري) - مثال: موقع غير واضح، عدد ركاب كبير...
        // Nullable لأنه قد لا يوجد سبب في بعض الحالات
        public ReviewReason? Reason { get; set; }

        // ملاحظات إضافية من الموظف (اختياري)
        [MaxLength(500)]
        public string? Notes { get; set; }

        // Foreign Key: الموظف/المستخدم الذي قام بالمراجعة (ApplicationUser.Id)
        [Required]
        public string ReviewedByUserId { get; set; } = default!;

        // وقت تنفيذ المراجعة
        public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;

        // ---------------- Navigation Properties ----------------

        // Navigation: الطلب الذي تمت مراجعته
        // العلاقة: Order (1) -> OrderReviews (Many)
        public Order Order { get; set; } = default!;

        // Navigation: المستخدم الذي قام بالمراجعة
        // العلاقة: ApplicationUser (1) -> OrderReviews (Many)
        public ApplicationUser ReviewedBy { get; set; } = default!;
    }
}
