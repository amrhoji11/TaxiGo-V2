using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Core.Models;
using TaxiApp.Backend.Infrastructure.Data;

namespace TaxiApp.Backend.Infrastructure.Repositories
{
    public class AdminRepository : IAdminRepository
    {
        private readonly ApplicationDbContext context;
        private readonly UserManager<ApplicationUser> userManager;

        public AdminRepository(ApplicationDbContext _context, UserManager<ApplicationUser> userManager)
        {
            context = _context;
            this.userManager = userManager;
        }

        public async Task<bool> UpdateAdminProfileAsync(string userId, UpdateAdminProfileDto request)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
                return false;

            // تعديل الاسم الأول والاسم الثاني
            if (!string.IsNullOrWhiteSpace(request.FirstName))
                user.FirstName = request.FirstName;

            if (!string.IsNullOrWhiteSpace(request.LastName))
                user.LastName = request.LastName;

            // تعديل رقم الهاتف
            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
                user.PhoneNumber = request.PhoneNumber;

            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return false;

            // تعديل العنوان
            if (request.RemoveAddress)
                user.Address = null;
            else if (!string.IsNullOrWhiteSpace(request.Address))
                user.Address = request.Address;

            // تعديل الصورة الشخصية
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "Images");

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            if (request.RemoveProfilePhoto)
            {
                if (!string.IsNullOrEmpty(user.ProfilePhotoImg))
                {
                    var oldPath = Path.Combine(folderPath, user.ProfilePhotoImg);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                user.ProfilePhotoImg = null;
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

                if (!string.IsNullOrEmpty(user.ProfilePhotoImg))
                {
                    var oldPath = Path.Combine(folderPath, user.ProfilePhotoImg);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                user.ProfilePhotoImg = fileName;
            }

            await context.SaveChangesAsync();

            return true;
        }

        public async Task<ApplicationUser?> GetAdminProfileAsync(string adminId)
        {
            return await userManager.FindByIdAsync(adminId);
        }

        public async Task<bool> SoftDeleteDriverAsync(string driverId)
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Id == driverId);
            if (user == null) return false;

            user.IsDeleted = true;

            var driver = await context.Drivers.FirstOrDefaultAsync(d => d.UserId == driverId);
            if (driver != null)
            {
                driver.IsDeleted = true;
            }

            await context.SaveChangesAsync();
            return true;
        }


        public async Task<List<Driver>> GetActiveDriversAsync()
        {
            return await context.Drivers
                .Where(d => !d.IsDeleted) // تجاهل السائقين المحذوفين
                .ToListAsync();
        }


        public async Task RestoreDriverAsync(string driverId)
        {
            var user = await context.Users
                                    .IgnoreQueryFilters()
                                    .FirstOrDefaultAsync(u => u.Id == driverId);

            if (user != null)
            {
                user.IsDeleted = false;
                await context.SaveChangesAsync();
            }

            var driver = await context.Drivers
                                      .IgnoreQueryFilters()
                                      .FirstOrDefaultAsync(d => d.UserId == driverId);

            if (driver != null)
            {
                driver.IsDeleted = false;
                await context.SaveChangesAsync();
            }
        }


        public async Task<bool> SoftDeletePassengerAsync(string passengerId)
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Id == passengerId);
            if (user == null) return false;

            user.IsDeleted = true;

            var passenger = await context.Passengers.FirstOrDefaultAsync(p => p.UserId == passengerId);
            if (passenger != null)
            {
                passenger.IsDeleted = true;
            }

            await context.SaveChangesAsync();
            return true;
        }
        public async Task<List<PassengerDto>> GetActivePassengersAsync()
        {
            return await context.Passengers
                .Include(p => p.User) // جلب بيانات المستخدم المرتبط
                .Where(p => !p.IsDeleted)
                .Select(p => new PassengerDto
                {
                    UserId = p.UserId,
                    FullName = p.User.FirstName + " " + p.User.LastName,
                    PhoneNumber = p.User.PhoneNumber,
                    Address = p.Address,
                    ProfilePhotoUrl = p.ProfilePhotoUrl,
                    UpdatedAt = p.UpdatedAt
                })
                .ToListAsync();
        }



        public async Task RestorePassengerAsync(string passengerId)
        {
            var user = await context.Users
                                    .IgnoreQueryFilters()
                                    .FirstOrDefaultAsync(u => u.Id == passengerId);

            if (user != null)
            {
                user.IsDeleted = false;
                await context.SaveChangesAsync();
            }

            var passenger = await context.Passengers
                                         .IgnoreQueryFilters()
                                         .FirstOrDefaultAsync(p => p.UserId == passengerId);

            if (passenger != null)
            {
                passenger.IsDeleted = false;
                await context.SaveChangesAsync();
            }
        }


        public async Task<PassengerProfileDto> GetPassengerProfileAsync(string passengerId)
        {
            var passenger = await context.Passengers
                .Include(p => p.User) // إذا عندك علاقة مع جدول Users
                .FirstOrDefaultAsync(p => p.UserId == passengerId && !p.IsDeleted);

            if (passenger == null) return null;

            return new PassengerProfileDto
            {
                Id = passenger.UserId,
                FullName = passenger.User.FirstName + " " + passenger.User.LastName,
                PhoneNumber = passenger.User.PhoneNumber,
                ProfileImageUrl = passenger.ProfilePhotoUrl
            };



        }


        public async Task<PagedResult<OrderDto>> GetOrdersAsync(
   int page,
  int pageSize,
  OrderStatus? status,
  string? search,
  OrderSortBy? sortBy,
  bool? ascending,
  DateTime? fromDate,
  DateTime? toDate)
        {
            if (toDate.HasValue)
                toDate = toDate.Value.Date.AddDays(1).AddTicks(-1);

            var query = context.Orders
                .Include(o => o.Passenger)
                .Include(o => o.TripOrders)
                .ThenInclude(to => to.Trip)
                .Include(o => o.Reviews)
                .AsQueryable();

            // 🔥 Filter by Status
            if (status.HasValue)
            {
                query = query.Where(o => o.Status == status.Value);
            }

            // 🔍 Search
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(o =>
                    o.Passenger.User.FirstName.Contains(search) ||
                    o.Passenger.User.LastName.Contains(search) ||
                    o.PickupLocation.Contains(search) ||
                    o.DropoffLocation.Contains(search));
            }

            if (fromDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt <= toDate.Value);
            }

            // 🔃 Sorting
            query = sortBy switch
            {
                OrderSortBy.PassengerName => (ascending ?? true)
                    ? query.OrderBy(o => o.Passenger.User.FirstName)
                    : query.OrderByDescending(o => o.Passenger.User.FirstName),

                OrderSortBy.Status => (ascending ?? true)
                    ? query.OrderBy(o => o.Status)
                    : query.OrderByDescending(o => o.Status),

                _ => (ascending ?? true)
                    ? query.OrderBy(o => o.CreatedAt)
                    : query.OrderByDescending(o => o.CreatedAt)
            };

            var totalCount = await query.CountAsync();

            var ratings = context.Ratings.AsQueryable();

            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new OrderDto
                {
                    OrderId = o.OrderId,

                    PassengerName = o.Passenger.User.FirstName + " " + o.Passenger.User.LastName,

                    PickupLocation = o.PickupLocation,
                    DropoffLocation = o.DropoffLocation,
                    PassengerCount = o.PassengerCount,
                    Status = o.Status,

                    TripId = o.TripOrders
                        .Select(t => t.TripId)
                        .FirstOrDefault(),

                    Rating = ratings
    .Where(r => r.OrderId == o.OrderId)
    .Select(r => new OrderRatingDto
    {
        Stars = (int?)r.Stars,
        Comment = r.Comment
    })
    .FirstOrDefault(),

                    CreatedAt = o.CreatedAt
                })
                .ToListAsync();

            return new PagedResult<OrderDto>
            {
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                Data = data
            };
        }


        public async Task<PagedResult<TripDto>> GetTripsAsync(
    int page,
    int pageSize,
    TripStatus? status,
    string? search,
    TripSortBy? sortBy,
    bool? ascending,
    DateTime? fromDate,
    DateTime? toDate)
        {

            if (toDate.HasValue)
                toDate = toDate.Value.Date.AddDays(1).AddTicks(-1);

            var query = context.Trips
                .Include(t => t.Driver)
                    .ThenInclude(d => d.User)
                .Include(t => t.TripOrders)
                    .ThenInclude(to => to.Order)
                        .ThenInclude(o => o.Passenger)
                .Include(t => t.Ratings)
                .AsQueryable();

            // 🔥 Filter
            if (status.HasValue)
            {
                query = query.Where(t => t.Status == status.Value);
            }

            // 🔍 Search
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(t =>
                    t.Driver.User.FirstName.Contains(search) ||
                    t.Driver.User.LastName.Contains(search));
            }

            if (fromDate.HasValue)
            {
                query = query.Where(t => t.CreatedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(t => t.CreatedAt <= toDate.Value);
            }

            // 🔃 Sorting
            query = sortBy switch
            {
                TripSortBy.DriverName => (ascending ?? true)
                    ? query.OrderBy(t => t.Driver.User.FirstName)
                    : query.OrderByDescending(t => t.Driver.User.FirstName),

                TripSortBy.Status => (ascending ?? true)
                    ? query.OrderBy(t => t.Status)
                    : query.OrderByDescending(t => t.Status),

                TripSortBy.CreatedAt => (ascending ?? true)
                    ? query.OrderBy(t => t.CreatedAt)
                    : query.OrderByDescending(t => t.CreatedAt)
            };

            var totalCount = await query.CountAsync();

            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TripDto
                {
                    TripId = t.TripId,
                    Status = t.Status,

                    DriverName = t.Driver.User.FirstName + " " + t.Driver.User.LastName,

                    TotalPassengers = t.TripOrders
                        .Sum(o => o.Order.PassengerCount),

                    // ✅ عدد التقييمات
                    RatingCount = t.Ratings.Count(),

                    TripRating = t.Ratings
                        .Select(r => (double?)r.Stars)
                        .Average() ?? 0,

                    IsActive = t.Status == TripStatus.Assigned ||
                               t.Status == TripStatus.InProgress,

                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            return new PagedResult<TripDto>
            {
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                Data = data
            };
        }


        public async Task<List<TopDriverDto>> GetTopDriversAsync(int top, DateTime? fromDate, DateTime? toDate)
        {
            var query = context.Trips
                .Where(t => t.Status == TripStatus.Completed)
                .AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(t => t.CreatedAt >= fromDate.Value.Date);

            if (toDate.HasValue)
                query = query.Where(t => t.CreatedAt <= toDate.Value.Date.AddDays(1).AddTicks(-1));

            // 🔥 1. جمع violations مرة واحدة (Performance Fix)
            var violations = await context.Violations
                .Where(v => v.Status == ViolationStatus.Active)
                .GroupBy(v => v.DriverId)
                .Select(g => new
                {
                    DriverId = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.DriverId, x => x.Count);

            var topDrivers = await query
         .GroupBy(t => t.DriverId)
         .Select(g => new
         {
             DriverId = g.Key,

             DriverName = g.First().Driver != null && g.First().Driver.User != null
                 ? g.First().Driver.User.FirstName + " " + g.First().Driver.User.LastName
                 : "Unknown",

             CompletedTrips = g.Count(),

             AvgRating = g.SelectMany(t => t.Ratings)
                 .Average(r => (double?)r.Stars) ?? 0
         })
         .ToListAsync();

            // 🔥 2. الحساب النهائي داخل memory (أسرع + أذكى)
            var result = topDrivers
                .Select(x =>
                {
                    violations.TryGetValue(x.DriverId, out int violationCount);

                    var tripsScore = Math.Min(x.CompletedTrips / 50.0, 1);
                    var ratingScore = x.AvgRating / 5.0;
                    var violationScore = Math.Pow(1 - Math.Min(violationCount / 10.0, 1), 1.3);

                    var score =
                        (ratingScore * 0.6) +
                        (tripsScore * 0.3) +
                        (violationScore * 0.1);

                    return new TopDriverDto
                    {
                        DriverId = x.DriverId,
                        DriverName = x.DriverName,
                        CompletedTrips = x.CompletedTrips,
                        AvgRating = x.AvgRating,
                        ViolationsCount = violationCount,
                        Score = score
                    };
                })
                .OrderByDescending(x => x.Score)
                .Take(top)
                .ToList();

            return result;
        }

    }
}
