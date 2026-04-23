using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.Models
{
    public enum TripOrderStatus
    {
        Assigned = 0,     // الطلب ضمن الرحلة
        PickedUp = 1,     // تم ركوب الراكب
        DroppedOff = 2,   // تم إنزال الراكب
        Cancelled = 3,     // هذا الطلب انلغى داخل الرحلة
        Unassigned = 4,
        DriverArrived=5

    }
    public class TripOrder
    {
        [ForeignKey(nameof(Trip))]
        public int TripId { get; set; }

        [ForeignKey(nameof(Order))]
        public int OrderId { get; set; }

        // ---------------- Tracking / Timing ----------------

        // وقت ربط هذا الطلب بهذه الرحلة (متى تم Assign Order داخل Trip)
        public DateTime AssignedAt { get; set; }

        // وقت فك الربط (إذا تم إلغاء ربط الطلب من الرحلة أو نقل الطلب لرحلة ثانية)
        // Nullable لأنه ممكن ما تم فك الربط أبداً
        public DateTime? UnassignedAt { get; set; }

        // حالة الطلب داخل الرحلة نفسها
        // مثال: Assigned / PickedUp / DroppedOff / Cancelled ...
        // تختلف عن OrderStatus لأنها مرتبطة بالرحلة وليس بالطلب بشكل عام
        public TripOrderStatus StatusInTrip { get; set; }


      

        // ---------------- Navigation Properties (Relationships) ----------------

        // Navigation Property: الوصول إلى بيانات الرحلة
        // العلاقة: Trip (1) -> TripOrders (Many)
        public Trip Trip { get; set; }

        // Navigation Property: الوصول إلى بيانات الطلب
        // العلاقة: Order (1) -> TripOrders (Many)
        public Order Order { get; set; }
    }
}
