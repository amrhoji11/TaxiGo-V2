using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.Models
{
    public enum OrderPriority
    {
        Normal = 0,     // عادي
        Urgent = 1,     // مستعجل
    }

    public enum OrderStatus
    {
        Pending = 0,
        SearchingDriver = 1,
        PendingOfficeReview = 2,
        AssignedToTrip = 3,
        Cancelled = 4,
        Completed = 5,
        NoDriverFound=6
    }


    public class Order
    {
        [Key]
        public int OrderId { get; set; }

        // Foreign Key: يربط الطلب بالراكب (Passenger) الذي أنشأ الطلب
        [ForeignKey(nameof(Passenger))]
        public string PassengerId { get; set; }

        // إحداثيات موقع الالتقاط (Pickup) - خط العرض Latitude
        public decimal PickupLat { get; set; }

        // إحداثيات موقع الالتقاط (Pickup) - خط الطول Longitude
        public decimal PickupLng { get; set; }

        // إحداثيات موقع الوصول (Dropoff) - خط العرض (ممكن تكون null إذا ما حددها المستخدم)
        public decimal? DropoffLat { get; set; }

        // إحداثيات موقع الوصول (Dropoff) - خط الطول (ممكن تكون null)
        public decimal? DropoffLng { get; set; }

        // وصف/اسم موقع الالتقاط (مثلاً: "شارع الجامعة - مقابل المكتبة")
        public string PickupLocation { get; set; }

        // وصف/اسم موقع الوصول (مثلاً: "مطار دمشق الدولي")
        public string DropoffLocation { get; set; }

        // أولوية الطلب (مثلاً: عادي - مستعجل - VIP حسب enum OrderPriority)
        public OrderPriority Priority { get; set; }

        // حجم السيارة المطلوبة للطلب (nullable: ممكن المستخدم ما يحدد الحجم)
        public Enums? RequiredVehicleSize { get; set; }

        // عدد الركاب في هذا الطلب
        public int PassengerCount { get; set; }

        // وقت إنشاء الطلب من ناحية المستخدم (وقت الطلب نفسه)
        public DateTime OrderTime { get; set; }

        // حالة الطلب (Pending / Accepted / InProgress / Completed / Cancelled ... حسب enum OrderStatus)
        public OrderStatus Status { get; set; }

        // هل الطلب يحتاج مراجعة من المكتب/الإدارة قبل تعيين سائق؟
        public bool NeedsOfficeReview { get; set; }

        // وقت إنشاء سجل الطلب داخل النظام (Database record creation time)
        public DateTime CreatedAt { get; set; }

        // آخر وقت تم تعديل الطلب فيه (nullable لأنه ممكن ما تم تعديل الطلب)
        public DateTime? UpdatedAt { get; set; }

        // وقت إلغاء الطلب (nullable لأنه قد لا يتم إلغاء الطلب)
        public DateTime? CancelledAt { get; set; }

        public DateTime? TripOfferSentAt { get; set; }
        public string? LastOfferedDriverId { get; set; }

        public bool IsManuallyAssigned { get; set; } = false;

        public DateTime? ExpectedArrivalAt { get; set; } // الوقت المتوقع للوصول
        public bool IsDelayNotified { get; set; } = false; // هل تم إرسال إشعار التأخير؟


        // ---------------- Navigation Properties ----------------

        // Navigation Property: الوصول إلى بيانات الراكب صاحب الطلب
        // العلاقة: Passenger (1) -> Orders (Many)
        public Passenger Passenger { get; set; }

        // علاقة Many-to-Many بين Trip و Order عن طريق جدول وسيط TripOrder
        // الطلب يمكن أن يكون ضمن رحلة (Trip) أو أكثر حسب تصميمك
        public ICollection<TripOrder> TripOrders { get; set; }=new List<TripOrder>();
        public ICollection<OrderReview> Reviews { get; set; } = new List<OrderReview>();
        public ICollection<Message> Messages { get; set; } = new List<Message>();

    }
}
