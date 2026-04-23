using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.Models

{
    
    public enum TripStatus
    {
        Pending = 0,        // تم إنشاء الرحلة (لسا ما انربطت بسائق)
        Assigned = 1,       // تم تعيين سائق للرحلة
        DriverArrived = 2,  // السائق وصل لنقطة الالتقاط
        InProgress = 3,     // الرحلة بدأت
        Completed = 4,      // الرحلة انتهت
        Cancelled = 5,// الرحلة انلغت
        SearchingDriver =6 ,
        NoDriverFound=7
    }

    public class Trip
    {
        [Key]
        public int TripId { get; set; }

        // Foreign Key: يربط الرحلة بالسائق الذي نفّذ/سيُنفّذ الرحلة
        // غالباً يشير إلى Driver.UserId
        public string? DriverId { get; set; }

        // وقت تعيين السائق لهذه الرحلة (متى تم ربط Trip بسائق)
        public DateTime? AssignedAt { get; set; }

        // وقت بداية الرحلة الفعلي (مثلاً لما السائق ضغط "Start Trip")
        public DateTime? StartTime { get; set; }

        // وقت وصول السائق إلى نقطة الالتقاط (Pickup) / وصوله للراكب
        public DateTime? DriverArrivedAt { get; set; }

        // وقت انتهاء الرحلة (لما السائق ضغط "End Trip")
        public DateTime? EndTime { get; set; }

        // حالة الرحلة (Pending / Assigned / InProgress / Completed / Cancelled ... حسب enum TripStatus)
        public TripStatus Status { get; set; }

        // وقت إنشاء سجل الرحلة في النظام (Database record creation time)
        public DateTime CreatedAt { get; set; }= DateTime.UtcNow;

        // آخر وقت تم تحديث بيانات الرحلة فيه (nullable لأنه ممكن ما صار تحديث)
        public DateTime? UpdatedAt { get; set; }

        public DateTime? CompletedAt { get; set; }
        public DateTime? TripOfferSentAt { get; set; }
        public string? LastOfferedDriverId { get; set; }

        public bool IsManuallyAssigned { get; set; } = false;
        public DateTime? ExpectedArrivalAt { get; set; } // الوقت المتوقع للوصول
        public bool IsDelayNotified { get; set; } = false; // هل تم إرسال إشعار التأخير؟
        // ---------------- Navigation Properties (العلاقات) ----------------

        // Navigation Property: الوصول لبيانات السائق المرتبط بهذه الرحلة
        // العلاقة: Driver (1) -> Trips (Many)
        public Driver? Driver { get; set; }

        // جدول وسيط لربط الرحلة بالطلبات (Trip <-> Order)
        // يسمح أن الرحلة تحتوي أكثر من Order (إذا عندك تجميع طلبات / pooling)
        // والعكس: Order يمكن يرتبط برحلة (أو أكثر حسب تصميمك)
        public ICollection<TripOrder> TripOrders { get; set; } = new List<TripOrder>();

        // تقييمات الرحلة (من الراكب أو أكثر، حسب تصميم Rating)
        // العلاقة غالباً: Trip (1) -> Ratings (Many)
        public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
        public ICollection<DriverLocation> Locations { get; set; } = new List<DriverLocation>();
        public ICollection<Message> Messages { get; set; } = new List<Message>();




    }
}
