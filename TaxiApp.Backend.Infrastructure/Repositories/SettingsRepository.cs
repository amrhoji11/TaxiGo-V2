using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Core.Models;
using TaxiApp.Backend.Infrastructure.Data;

namespace TaxiApp.Backend.Infrastructure.Repositories
{
    public class SettingsRepository :ISettingsRepository
    {
        private readonly ApplicationDbContext context;
        private readonly UserManager<ApplicationUser> userManager;
        

        
        public SettingsRepository(ApplicationDbContext _context, UserManager<ApplicationUser> userManager)
        {
            context = _context;
            this.userManager = userManager;
            
        }
        public async Task<bool> UpdateLanguageAsync(string userId, string language)
        {
            var user = await context.Users.FindAsync(userId);
            if (user == null) return false;

            user.Language = language; // لازم يكون عندك عمود Language في جدول Users
            await context.SaveChangesAsync();
            return true;
        }

        //  عرض اللغة
        public async Task<string?> GetLanguageAsync(string userId)
        {
            var user = await context.Users.FindAsync(userId);
            return user?.Language;
        }


        public async Task<bool> UpdateNotificationsAsync(string userId, bool enabled)
        {
            var user = await context.Users.FindAsync(userId);
            if (user == null) return false;

            user.NotificationsEnabled = enabled;
            await context.SaveChangesAsync();
            return true;
        }

        public async Task<bool?> GetNotificationsStatusAsync(string userId)
        {
            var user = await context.Users.FindAsync(userId);
            if (user == null) return null;

            return user.NotificationsEnabled;
        }

        //  تحديث الوضع الليلي
        public async Task<bool> UpdateDarkModeAsync(string userId, bool enabled)
        {
            var user = await context.Users.FindAsync(userId);
            if (user == null) return false;

            user.IsDarkModeEnabled = enabled;
            await context.SaveChangesAsync();
            return true;
        }

        //  عرض الوضع الليلي
        public async Task<bool?> GetDarkModeAsync(string userId)
        {
            var user = await context.Users.FindAsync(userId);
            return user?.IsDarkModeEnabled;
        }


      

    }
}
