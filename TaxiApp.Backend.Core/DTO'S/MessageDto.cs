using System;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class MessageDto
    {
        public int MessageId { get; set; }
        public string SenderUserId { get; set; }
        public string SenderName { get; set; }
        public string? SenderProfilePhoto { get; set; }
        public string ReceiverUserId { get; set; }
        public int? OrderId { get; set; }
        public int? TripId { get; set; }
        public string Body { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }
    }
}
