using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.Models
{
    public class Message
    {
        [Key]
        public int MessageId { get; set; }

        // Foreign Key: المرسل (ApplicationUser.Id)
        [Required]
        public string SenderUserId { get; set; }

        // Foreign Key: المستلم (ApplicationUser.Id)
        [Required]
        public string ReceiverUserId { get; set; }

        // Foreign Key (اختياري): إذا كانت الرسالة تخص طلب معين
        // Nullable لأن مو كل الرسائل مرتبطة بطلب
        public int? OrderId { get; set; }

        // Foreign Key (اختياري): إذا كانت الرسالة تخص رحلة معينة
        // Nullable لأن مو كل الرسائل مرتبطة برحلة
        public int? TripId { get; set; }

        // نص الرسالة
        [Required]
        [MaxLength(1000)]
        public string Body { get; set; }

        // وقت إرسال الرسالة (يفضّل تخزينه بـ UTC)
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        // هل المستلم قرأ الرسالة؟
        public bool IsRead { get; set; } = false;

        // ---------------- Navigation Properties ----------------

        // Navigation: بيانات المرسل
        public ApplicationUser Sender { get; set; }

        // Navigation: بيانات المستلم
        public ApplicationUser Receiver { get; set; }

        public Order? Order { get; set; }
        public Trip? Trip { get; set; }
    }
}
