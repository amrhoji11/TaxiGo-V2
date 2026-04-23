using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Core.Models;
using TaxiApp.Backend.Infrastructure.Data;

namespace TaxiApp.Backend.Infrastructure.Repositories
{
    public class DriverApprovalRepository : IDriverApprovalRepository
    {
        private readonly ApplicationDbContext _context;

        public DriverApprovalRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DriverPendingResponseListDto> GetPendingDriversAsync(int pageNumber = 1, int pageSize=10)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;

            var countDrivers = await _context.DriverApprovals.AsNoTracking().Where(a=>a.Status== ApprovalStatus.pending).CountAsync();

            // جلب السائقين مع بياناتهم الشخصية من جدول Users
            var drivers = await _context.DriverApprovals
        .Include(a => a.Driver)
            .ThenInclude(d => d.User)
        .Where(a => a.Status == ApprovalStatus.pending)
        .OrderBy(a=>a.CreatedAt)
        .Skip((pageNumber - 1) * pageSize).Take(pageSize)
        .Select(a => new DriverPendingResponseDto
        {
            UserId= a.DriverId,
            FullName= a.Driver.User.FirstName+" "+a.Driver.User.LastName,
            PhoneNumber=a.Driver.User.PhoneNumber
        })
        .ToListAsync();

            return new DriverPendingResponseListDto
            {
                Count = countDrivers,
                Drivers = drivers
            };

        }

        public async Task<bool> ApproveDriverAsync(string officeId,string driverId)
        {
            var approval = await _context.DriverApprovals
         .FirstOrDefaultAsync(a => a.DriverId == driverId);

            if (approval == null)
                return false;

            if (approval.Status != ApprovalStatus.pending)
                return false;


            approval.Status = ApprovalStatus.approved;
            approval.ReviewedByUserId = officeId;
            approval.ReviewedAt = DateTime.UtcNow;
            approval.Notes = null;

            // عند الموافقة نجعله Offline مبدئياً
            var driver = await _context.Drivers.FindAsync(driverId);
            if (driver != null)
                driver.Status = DriverStatus.offline;

            await _context.SaveChangesAsync();

            return true;
        }



        public async Task<bool> RejectDriverAsync(string officeId, string driverId, string? notes = null)
        {
            var approval = await _context.DriverApprovals
                .FirstOrDefaultAsync(a => a.DriverId == driverId);

            if (approval == null)
                return false;

            if (approval.Status != ApprovalStatus.pending)
                return false;

            var driver = await _context.Drivers.FindAsync(driverId);
            if (driver == null)
                return false;

            // 🔴 رفض السائق
            approval.Status = ApprovalStatus.rejected;
            approval.ReviewedByUserId = officeId;
            approval.ReviewedAt = DateTime.UtcNow;
            approval.Notes = notes ?? "No reason provided";
            driver.Status = DriverStatus.rejected;







            await _context.SaveChangesAsync();

            return true;
        }


        public async Task<object> GetDriverDetailsAsync(string driverId)
        {
            var driver = await _context.Drivers
                .Include(d => d.User)
                .Include(d => d.Vehicles)
                .Include(d => d.Trips)
                    .ThenInclude(t => t.Ratings)
                .FirstOrDefaultAsync(d => d.UserId == driverId);

            if (driver == null)
                return null;

            var totalTrips = driver.Trips.Count();

            var completedTrips = driver.Trips
                .Count(t => t.Status == TripStatus.Completed);

            var cancelledTrips = driver.Trips
                .Count(t => t.Status == TripStatus.Cancelled);

            var ratings = driver.Trips
                .SelectMany(t => t.Ratings);

            return new
            {
                DriverId = driver.UserId,

                Name = driver.User.FirstName + " " + driver.User.LastName,
                Email = driver.User.Email,
                Phone = driver.User.PhoneNumber,

                Status = driver.Status,
                CreatedAt = driver.User.CreatedAt,

                Vehicle = driver.Vehicles
    .Select(v => new
    {
        v.Model,
        v.PlateNumber
    })
    .FirstOrDefault(),

                TotalTrips = totalTrips,
                CompletedTrips = completedTrips,
                CancelledTrips = cancelledTrips,

                Rating = ratings.Any()
                    ? ratings.Average(r => r.Stars)
                    : 0,

                RatingCount = ratings.Count()
            };
        }
    }
}