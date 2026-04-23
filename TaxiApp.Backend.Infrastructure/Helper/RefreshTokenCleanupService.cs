using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Infrastructure.Data;

namespace TaxiApp.Backend.Infrastructure.Helper
{
    public class RefreshTokenCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory scopeFactory;

        public RefreshTokenCleanupService(IServiceScopeFactory scopeFactory)
        {
            this.scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

                var now = DateTime.UtcNow;

                var oldTokens = await context.RefreshTokens
                    .Where(t =>
                        t.ExpiresAt <= now ||
                        (t.IsRevoked && t.RevokedAt <= now.AddDays(-30)))
                    .ToListAsync(stoppingToken);

                if (oldTokens.Any())
                {
                    context.RefreshTokens.RemoveRange(oldTokens);
                    await context.SaveChangesAsync(stoppingToken);
                }

                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
        }

    }
}
