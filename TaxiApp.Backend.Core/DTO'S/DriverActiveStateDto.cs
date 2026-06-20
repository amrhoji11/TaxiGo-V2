using System;
using System.Collections.Generic;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.DTO_S
{
    /// <summary>
    /// What a driver client should be showing right now. Recovers state on
    /// app restart/reconnect — live updates still arrive over SignalR
    /// (NewTripOffer / UpdateTripStatus / ReceiveNotification), this is only
    /// the durable backstop for when those were missed.
    /// </summary>
    public enum DriverActiveStateType
    {
        Idle = 0,
        OfferPending = 1,
        OnTrip = 2
    }

    public class DriverActiveStateDto
    {
        public DriverActiveStateType State { get; set; }
        public DriverOrderOfferDto? Offer { get; set; }
        public DriverActiveTripDto? Trip { get; set; }
    }

    public class DriverOrderOfferDto
    {
        public int OrderId { get; set; }
        public decimal PickupLat { get; set; }
        public decimal PickupLng { get; set; }
        public string PickupLocation { get; set; }
        public decimal? DropoffLat { get; set; }
        public decimal? DropoffLng { get; set; }
        public string? DropoffLocation { get; set; }
        public int PassengerCount { get; set; }
        public OrderPriority Priority { get; set; }
        public Enums? RequiredVehicleSize { get; set; }
        public DateTime OfferExpiresAt { get; set; }
    }

    public class DriverActiveTripDto
    {
        public int TripId { get; set; }
        public TripStatus Status { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime? StartTime { get; set; }
        public List<DriverTripStopDto> Stops { get; set; } = new();
    }

    public class DriverTripStopDto
    {
        public int OrderId { get; set; }
        public TripOrderStatus StatusInTrip { get; set; }
        public decimal PickupLat { get; set; }
        public decimal PickupLng { get; set; }
        public string PickupLocation { get; set; }
        public decimal? DropoffLat { get; set; }
        public decimal? DropoffLng { get; set; }
        public string? DropoffLocation { get; set; }
        public int PassengerCount { get; set; }
        public string PassengerName { get; set; }
        public string? PassengerPhone { get; set; }
        public string? PassengerProfilePhotoUrl { get; set; }
    }
}
