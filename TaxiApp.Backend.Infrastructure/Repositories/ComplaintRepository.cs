using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Core.Models;
using TaxiApp.Backend.Core.Settings;
using TaxiApp.Backend.Infrastructure.Data;

namespace TaxiApp.Backend.Infrastructure.Repositories
{
    public class ComplaintRepository : IComplaintRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationRepository _notification;
        private readonly TaxiSettings _settings;

        public ComplaintRepository(ApplicationDbContext context,
            INotificationRepository notification, IOptions<TaxiSettings> settings)
        {
            _context = context;
            _notification = notification;
            _settings = settings.Value;
        }

        // =========================
        // CREATE COMPLAINT
        // =========================
        public async Task<string> CreateComplaintAsync(string userId,int orderId ,CreateComplaintDto dto)
        {
            // 1️⃣ Order + Trip
            var order = await _context.Orders
                .Include(o => o.TripOrders)
                .ThenInclude(t => t.Trip)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                return "Order not found";

            // 2️⃣ Ownership
            if (order.PassengerId != userId)
                return "Unauthorized";

            var trip = await _context.Trips
    .Include(t => t.Driver)
    .Include(t => t.TripOrders)
    .FirstOrDefaultAsync(t => t.TripOrders.Any(o => o.OrderId == orderId));

            if (trip == null)
                return "No trip yet";

            if (trip.DriverId == null)
                return "Driver not assigned yet";

            // 3️⃣ Validation
            if (order.Status == OrderStatus.Pending)
                return "Cannot complain before driver assigned";

            if (trip.Status == TripStatus.SearchingDriver)
                return "Trip not started";

            if (trip.Status != TripStatus.InProgress &&
                trip.Status != TripStatus.Completed)
                return "Invalid complaint state";

            if (trip.Status == TripStatus.Completed &&
                trip.CompletedAt.HasValue &&
                trip.CompletedAt.Value < DateTime.UtcNow.AddHours(-24))
            {
                return "Complaint time expired";
            }

            // 4️⃣ Create Complaint
            var complaint = new Complaint
            {
                SenderId = userId,
                AgainstUserId = trip.DriverId,
                OrderId = order.OrderId,
                TripId = trip.TripId,
                TargetType = dto.TargetType,
                ReasonType=dto.ReasonType,
                Description = dto.Description
            };

            _context.Complaints.Add(complaint);
            await _context.SaveChangesAsync();




            // 🔔 Notification
            await _notification.SendOfficeNotificationAsync(
     officeUserId: _settings.OfficeUserId,
     type: NotificationType.Complaint,
     title: "New Complaint",
     body: $"Complaint on Order #{order.OrderId}",
     orderId: order.OrderId
 );
            return "Complaint submitted";
        }

        // =========================
        // GET ALL
        // =========================
        public async Task<List<Complaint>> GetAllComplaintsAsync()
        {
            return await _context.Complaints
                .Include(c => c.Violation)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        // =========================
        // UPDATE STATUS
        // =========================
        public async Task<string> UpdateStatusAsync(int ComplaintId, UpdateComplaintStatusDto dto)
        {
            var complaint = await _context.Complaints
                .FirstOrDefaultAsync(c => c.Id == ComplaintId);

            if (complaint == null)
                return "Complaint not found";

            complaint.Status = dto.Status;

            // ✅ أولاً: حل الشكوى
            if (dto.Status == ComplaintStatus.Resolved)
                complaint.ResolvedAt = DateTime.UtcNow;

            // ✅ ثانياً: إنشاء مخالفة (فقط إذا الشكوى انحلت)
            if (dto.Status == ComplaintStatus.Resolved &&
                dto.CreateViolation &&
                !string.IsNullOrEmpty(complaint.AgainstUserId))
            {
                var violation = new Violation
                {
                    DriverId = complaint.AgainstUserId,
                    TripId=complaint.TripId,
                    OrderId=complaint.OrderId,
                    Reason = dto.ViolationReason ?? "Complaint violation",
                    Status = ViolationStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    Type=dto.ViolationType
                };

                _context.Violations.Add(violation);
                await _context.SaveChangesAsync();

                // 🔥 حساب عدد المخالفات
                var violationCount = await _context.Violations
                    .CountAsync(v => v.DriverId == complaint.AgainstUserId
                                  && v.Status == ViolationStatus.Active);

                // 🔥 جلب السائق
                var driver = await _context.Drivers
                    .FirstOrDefaultAsync(d => d.UserId == complaint.AgainstUserId);

                if (driver != null)
                {
                    // 🔔 إشعار دائم عند كل مخالفة
                    await _notification.SendNotificationAsync(
                        driver.UserId,
                        NotificationType.Violation,
                        "⚠️ تم تسجيل مخالفة عليك",
                        $"لديك الآن {violationCount} مخالفات",
                        null,
                        null
                    );

                    
                   



                    await _context.SaveChangesAsync();


                }

                complaint.ViolationId = violation.Id;

                await _context.SaveChangesAsync();
            }

            await _notification.SendNotificationAsync(
                complaint.SenderId,
                NotificationType.Complaint,
                "Complaint Updated",
                $"Status: {complaint.Status}"
            );

            return "Updated";
        }
        // =========================
        // DRIVER VIOLATIONS COUNT
        // =========================
        public async Task<int> GetDriverViolationsCountAsync(string driverId)
        {
            return await _context.Violations
                .CountAsync(v => v.DriverId == driverId && v.Status == ViolationStatus.Active);
        }

        // =========================
        // GET VIOLATIONS
        // =========================
        public async Task<List<Violation>> GetAllViolationsAsync()
        {
            return await _context.Violations
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();
        }


        public async Task<string> ResolveViolationAsync(int violationId)
        {
            var violation = await _context.Violations
                .FirstOrDefaultAsync(v => v.Id == violationId);

            if (violation == null)
                return "Violation not found";

            if (violation.Status == ViolationStatus.Resolved)
                return "Already resolved";

            violation.Status = ViolationStatus.Resolved;
            violation.ResolvedAt = DateTime.UtcNow;

           





            await _context.SaveChangesAsync();

            return "Violation resolved";
        }
    }
}
