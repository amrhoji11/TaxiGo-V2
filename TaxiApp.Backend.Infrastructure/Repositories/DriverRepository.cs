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
    public class DriverRepository : IDriverRepository
    {
        private readonly ApplicationDbContext context;
        private readonly UserManager<ApplicationUser> userManager;

        public DriverRepository(ApplicationDbContext _context,UserManager<ApplicationUser> userManager)
        {
            context = _context;
            this.userManager = userManager;
        }

       
        public async Task<bool> UpdateDriverProfileAsync(string userId, UpdateDriverRequest request)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
                return false;

            var driver = await context.Drivers
                .FirstOrDefaultAsync(d => d.UserId == userId);

            if (driver == null)
                return false;

            if (!string.IsNullOrWhiteSpace(request.FirstName))
                user.FirstName = request.FirstName;

            if (!string.IsNullOrWhiteSpace(request.LastName))
                user.LastName = request.LastName;

           

            if (request.RemoveAddress)
            {
                
                user.Address = null;
            }
            else if (!string.IsNullOrWhiteSpace(request.Address))
            {
                
                user.Address = request.Address;
            }

            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return false;

            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "Images");

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            if (request.RemoveProfilePhoto)
            {
                if (!string.IsNullOrEmpty(driver.ProfilePhotoUrl))
                {
                    var oldPath = Path.Combine(folderPath, driver.ProfilePhotoUrl);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                driver.ProfilePhotoUrl = null;
            }
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

                if (!string.IsNullOrEmpty(driver.ProfilePhotoUrl))
                {
                    var oldPath = Path.Combine(folderPath, driver.ProfilePhotoUrl);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                driver.ProfilePhotoUrl = fileName;
            }

            driver.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();

            return true;
        }


        public async Task<List<DriverTripReportDto>> GetDriverTripsReportAsync(string driverId, DateTime? from, DateTime? to)
        {
            // جلب الرحلات للسائق + التفاصيل + التقييمات
            var trips = await context.TripOrders
                .Include(t => t.Order)
                    .ThenInclude(o => o.Passenger)
                .Include(t => t.Trip)
                .Where(t => t.Trip.DriverId == driverId &&
                            t.Trip.CompletedAt != null &&
                            (!from.HasValue||t.Trip.CompletedAt.Value.Date >= from.Value.Date )&&
                            (!to.HasValue || t.Trip.CompletedAt.Value.Date <= to.Value.Date))
                .ToListAsync();

            // جلب كل تقييمات الركاب مرة واحدة
            var orderIds = trips.Select(t => t.OrderId).ToList();
            var allRatings = await context.Ratings
                .Where(r => orderIds.Contains(r.OrderId) && r.RaterUserId != driverId) // التقييم من الركاب فقط
                .ToListAsync();

            var report = trips.Select(t =>
            {
                TimeSpan duration = TimeSpan.Zero;
                if (t.Trip.StartTime != null && t.Trip.CompletedAt != null)
                    duration = t.Trip.CompletedAt.Value - t.Trip.StartTime.Value;

                var rating = allRatings.FirstOrDefault(r => r.OrderId == t.OrderId);

                return new DriverTripReportDto
                {
                    PassengerName = t.Order.Passenger != null
                        ? $"{t.Order.Passenger.User.FirstName} {t.Order.Passenger.User.LastName}"
                        : "Unknown",
                    PickupLocation = t.Order?.PickupLocation ?? "N/A",
                    Destination = t.Order?.DropoffLocation ?? "N/A",
                    Duration = duration,
                    Rating = rating?.Stars,
                    Comment = rating?.Comment,
                    CompletedAt = t.Trip.CompletedAt ?? DateTime.MinValue
                };
            }).ToList();

            return report;
        }


       
    }
}
