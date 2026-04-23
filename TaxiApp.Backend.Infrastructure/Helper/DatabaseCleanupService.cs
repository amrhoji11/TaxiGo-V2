using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.Models;
using TaxiApp.Backend.Infrastructure.Data;

namespace TaxiApp.Backend.Infrastructure.Helper
{
    public class DatabaseCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
       

        public DatabaseCleanupService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
            
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await CleanDatabase();

                // تشغيل مرة كل 24 ساعة
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }

        private async Task CleanDatabase()
        {
            using var scope = _scopeFactory.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var locationThreshold = DateTime.UtcNow.AddDays(-1);
            var notificationThreshold = DateTime.UtcNow.AddDays(-3);

            await context.DriverLocations
                .Where(x => x.RecordedAt < locationThreshold)
                .ExecuteDeleteAsync();

            await context.Notifications
                .Where(x => x.CreatedAt < notificationThreshold)
                .ExecuteDeleteAsync();

            var complaintThreshold = DateTime.UtcNow.AddMonths(-3);

            await context.Complaints
                .Where(c =>
                    c.Status == ComplaintStatus.Resolved &&
                    c.ResolvedAt != null &&
                    c.ResolvedAt < complaintThreshold)
                .ExecuteDeleteAsync();

            await context.Violations
                .Where(v =>
                    v.Status == ViolationStatus.Resolved &&
                    v.ResolvedAt != null &&
                    v.ResolvedAt < complaintThreshold)
                .ExecuteDeleteAsync();
        }
    }
}
