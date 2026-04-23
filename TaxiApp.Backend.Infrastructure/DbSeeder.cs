using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.Models;
using TaxiApp.Backend.Infrastructure.Data;

namespace TaxiApp.Backend.Infrastructure
{
    public static class DbSeeder
    {
        public static async Task SeedAdminAsync(IServiceProvider serviceProvider)
        {
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var adminPhone = "0595541748"; // رقم هاتف الـ Office الافتراضي

            var adminUser = await userManager.FindByNameAsync(adminPhone);

            if (adminUser == null)
            {
                var officeAdmin = new ApplicationUser
                {
                    FirstName="Amr",
                    LastName="Hoji",
                    UserName = "amrhoji",
                    PhoneNumber = adminPhone,
                    Email = "admin@taxiapp.com",
                    EmailConfirmed = true,
                    PhoneNumberConfirmed = true
                    // أضف أي حقول إضافية يحتاجها موديل الـ User عندك
                };

                var result = await userManager.CreateAsync(officeAdmin, "Admin@123"); // كلمة سر افتراضية
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(officeAdmin, "Admin");
                }
            }
        }
    }
}
