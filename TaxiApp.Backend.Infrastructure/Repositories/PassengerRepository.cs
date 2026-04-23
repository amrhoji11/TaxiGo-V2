using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.DTO_S.AuthDto.Requests;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Core.Models;
using TaxiApp.Backend.Infrastructure.Data;

namespace TaxiApp.Backend.Infrastructure.Repositories
{
    public class PassengerRepository : IPassengerRepository
    {
        private readonly ApplicationDbContext context;
        private readonly UserManager<ApplicationUser> userManager;

        public PassengerRepository(ApplicationDbContext _context, UserManager<ApplicationUser> userManager)
        {
            context = _context;
            this.userManager = userManager;
        }

       

        public async Task<bool> UpdatePassengerProfileAsync(string userId, UpdatePassengerRequest request)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
                return false;

            var passenger = await context.Passengers
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (passenger == null)
                return false;

            // تحديث الاسم
            if (!string.IsNullOrWhiteSpace(request.FirstName))
                user.FirstName = request.FirstName;

            if (!string.IsNullOrWhiteSpace(request.LastName))
                user.LastName = request.LastName;

            

            // تحديث / حذف العنوان
            if (request.RemoveAddress)
                user.Address = null;
            else if (!string.IsNullOrWhiteSpace(request.Address))
                user.Address = request.Address;

            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return false;

            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "Images");

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            // حذف صورة
            if (request.RemoveProfilePhoto)
            {
                if (!string.IsNullOrEmpty(passenger.ProfilePhotoUrl))
                {
                    var oldPath = Path.Combine(folderPath, passenger.ProfilePhotoUrl);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                passenger.ProfilePhotoUrl = null;
            }
            // رفع صورة جديدة
            else if (request.ProfilePhotoImg != null && request.ProfilePhotoImg.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                var extension = Path.GetExtension(request.ProfilePhotoImg.FileName).ToLower();

                if (!allowedExtensions.Contains(extension))
                    return false;

                var fileName = Guid.NewGuid() + extension;
                var filePath = Path.Combine(folderPath, fileName);

                using var stream = System.IO.File.Create(filePath);
                await request.ProfilePhotoImg.CopyToAsync(stream);

                if (!string.IsNullOrEmpty(passenger.ProfilePhotoUrl))
                {
                    var oldPath = Path.Combine(folderPath, passenger.ProfilePhotoUrl);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                passenger.ProfilePhotoUrl = fileName;
            }

            passenger.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();

            return true;
        }


        public async Task<string> RateDriverAsync(int orderId, string passengerId, int stars, string? comment)
        {
            if (stars < 1 || stars > 5)
                return "Stars must be between 1 and 5";

            var tripOrder = await context.TripOrders
                .Include(to => to.Trip)
                .Include(to => to.Order)
                .FirstOrDefaultAsync(to => to.OrderId == orderId);

            if (tripOrder == null)
                return "Order not found in any trip";

            var trip = tripOrder.Trip;

            if (trip == null || trip.Status != TripStatus.Completed)
                return "Trip not finished yet";

            if (tripOrder.StatusInTrip != TripOrderStatus.DroppedOff)
                return "You must complete the trip before rating";

            // ✅ تأكد إنو الراكب هو صاحب الطلب
            if (tripOrder.Order.PassengerId != passengerId)
                return "Unauthorized passenger";

            // 🔥 أهم نقطة: التقييم خلال 30 دقيقة فقط
            if (trip.CompletedAt == null ||
                (DateTime.UtcNow - trip.CompletedAt.Value).TotalMinutes > 30)
            {
                return "Rating time expired (30 minutes only)";
            }

            // ✅ منع التقييم مرتين
            bool alreadyRated = await context.Ratings
                .AnyAsync(r =>
                    r.OrderId == orderId &&
                    r.RaterUserId == passengerId);

            if (alreadyRated)
                return "You already rated this order";

            var rating = new Rating
            {
                OrderId = orderId,
                TripId = trip.TripId,
                RaterUserId = passengerId,
                TargetUserId = trip.DriverId!,
                Stars = stars,
                Comment = comment,
                RatedAt = DateTime.UtcNow
            };

            await context.Ratings.AddAsync(rating);
            await context.SaveChangesAsync();

            return "Rating submitted successfully";
        }



        public async Task<List<PassengerTripReportDto>> GetPassengerTripsReportAsync(
      string passengerId, DateTime from, DateTime to)
        {
            // 1. تحسين الاستعلام ليشمل المستخدم والتقييمات دفعة واحدة
            var trips = await context.TripOrders
                .Include(a => a.Order)
                .Include(a => a.Trip)
                    .ThenInclude(t => t.Driver)
                        .ThenInclude(d => d.User) // 🔥 هذا السطر يحل مشكلة الـ Null في الاسم
                .Where(a => a.Order.PassengerId == passengerId &&
                             a.Trip.CompletedAt != null &&
                             a.Trip.CompletedAt.Value.Date >= from.Date &&
                             a.Trip.CompletedAt.Value.Date <= to.Date)
                .ToListAsync();

            // جلب كل التقييمات لهذا الراكب مرة واحدة لتحسين الأداء
            var orderIds = trips.Select(x => x.OrderId).ToList();
            var allRatings = await context.Ratings
                .Where(r => orderIds.Contains(r.OrderId) && r.RaterUserId == passengerId)
                .ToListAsync();

            var report = trips.Select(to =>
            {
                TimeSpan duration = TimeSpan.Zero;
                if (to.Trip.StartTime != null && to.Trip.CompletedAt != null)
                    duration = to.Trip.CompletedAt.Value - to.Trip.StartTime.Value;

                // البحث في القائمة المحملة مسبقاً بدلاً من ضرب قاعدة البيانات مرة أخرى
                var rating = allRatings.FirstOrDefault(r => r.OrderId == to.OrderId);

                return new PassengerTripReportDto
                {
                    // استخدام ?. لضمان عدم حدوث خطأ حتى لو السائق أو المستخدم مفقود
                    DriverName = to.Trip.Driver?.User != null
                        ? $"{to.Trip.Driver.User.FirstName} {to.Trip.Driver.User.LastName}"
                        : "Unknown",

                    PickupLocation = to.Order?.PickupLocation ?? "N/A",
                    Destination = to.Order?.DropoffLocation ?? "N/A",
                    Duration = duration,
                    Rating = rating?.Stars,
                    Comment = rating?.Comment,
                    CompletedAt = to.Trip.CompletedAt ?? DateTime.MinValue
                };
            }).ToList();

            return report;
        }



      

       

    }
}
