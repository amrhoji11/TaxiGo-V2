using System;
using System.Text.Json.Serialization;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class NotificationDto
    {
        public int NotificationId { get; set; }

        // Serialized as the enum *name* (e.g. "DriverArrived"), not its
        // numeric value, to match the SignalR push shape — every existing
        // SendAsync("ReceiveNotification"/"UpdateTripStatus", ...) call
        // already sends `type.ToString()`. Keeping both the REST history
        // and the live push the same string shape means a client only
        // needs one NotificationType parser, not two.
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public NotificationType Type { get; set; }
        public int? OrderId { get; set; }
        public int? TripId { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
    }
}
