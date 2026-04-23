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
    public class FavoriteLocationsRepository : IFavoriteLocationsRepository
    {
        private readonly ApplicationDbContext context;

        public FavoriteLocationsRepository(ApplicationDbContext _context)
        {
            context = _context;
        }


        // إضافة موقع جديد
        public async Task AddLocationAsync(string userId, AddFavoriteLocationDto dto)
        {
            var location = new FavoriteLocation
            {
                UserId = userId,
                Name = dto.Name,
                Address = dto.Address,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude
            };

            context.FavoriteLocations.Add(location);
            await context.SaveChangesAsync();
        }

        // عرض المواقع المفضلة
        public async Task<List<FavoriteLocation>> GetLocationsAsync(string userId)
        {
            return await context.FavoriteLocations
                .Where(l => l.UserId == userId)
                .ToListAsync();
        }

        // حذف موقع مفضل
        public async Task<bool> DeleteLocationAsync(string userId, int locationId)
        {
            var location = await context.FavoriteLocations
                .FirstOrDefaultAsync(l => l.UserId == userId && l.Id == locationId);

            if (location == null) return false;

            context.FavoriteLocations.Remove(location);
            await context.SaveChangesAsync();
            return true;
        }

    }
}
