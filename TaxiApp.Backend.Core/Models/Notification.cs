using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.Models
{
    public enum NotificationType
    {
        //----------------- Rate -------------------
        RateTrip=0,
        // ---------------- Messages ----------------
        MessageReceived =1,

        // ---------------- Trip Notifications ----------------
        TripAssigned = 10,
        DriverArrived = 11,
        TripStarted = 12,
        TripCompleted = 13,
        DriverCancelledTrip = 14,
        NewTripOffer = 15,
        DriverRejectedTrip=16,
        DriverAcceptedTrip =17,
        PickedUp=18,



        // ---------------- Order Notifications ----------------
        OrderCreated=19,
        OrderCancelled = 20,
        OrderNeedsReview = 21,
        OrderReviewed = 22,
        NoDriverFound=23,
        DelayWarning=24,

        // ---------------- Driver Approval ----------------
        DriverApprovalPending = 30,   // يروح للمكتب: سائق جديد يحتاج موافقة
        DriverApproved = 31,          // يروح للسائق: تم قبولك
        DriverRejected = 32,          // يروح للسائق: تم رفضك

        // ---------------- Office Queue ----------------
        DriverEnteredQueue = 40,      // يروح للمكتب: سائق دخل الطابور
        DriverLeftQueue = 41  ,        // يروح للمكتب: سائق غادر الطابور


        //----------------violation----------------------

            Violation=50,
        Complaint=51
    }

    public class Notification
    {
        [Key]
        public int NotificationId { get; set; }

        // Foreign Key: المستخدم الذي سيستلم الإشعار (ApplicationUser.Id)
        [ForeignKey(nameof(ApplicationUser))]
        [Required]
        public string UserId { get; set; } = default!;

        // نوع الإشعار (مثلاً: رسالة جديدة، تغيير حالة طلب، تعيين سائق...)
        public NotificationType Type { get; set; }

        // إذا الإشعار مرتبط بطلب معين (اختياري)
        public int? OrderId { get; set; }

        // إذا الإشعار مرتبط برحلة معينة (اختياري)
        public int? TripId { get; set; }

        // عنوان الإشعار (قصير)
        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = default!;

        // نص الإشعار (تفاصيل أكثر)
        [Required]
        [MaxLength(1000)]
        public string Body { get; set; } = default!;

        // وقت إنشاء الإشعار
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // هل تم قراءة الإشعار؟
        public bool IsRead { get; set; } = false;

        // Navigation: بيانات المستخدم المستلم للإشعار
        public ApplicationUser User { get; set; }
        public Order? Order { get; set; }
        public Trip? Trip { get; set; }
    }
}
