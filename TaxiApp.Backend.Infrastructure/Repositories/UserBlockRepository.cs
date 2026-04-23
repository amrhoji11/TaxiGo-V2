using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Core.Models;
using TaxiApp.Backend.Infrastructure.Data;

namespace TaxiApp.Backend.Infrastructure.Repositories
{
    public class UserBlockRepository : IUserBlockRepository
    {
        private readonly ApplicationDbContext context;

        public UserBlockRepository(ApplicationDbContext context)
        {
            this.context = context;
        }

        public async Task<IEnumerable<AllBlocksDto?>> GetAllBlocks(int pageNumber=1, int pageSize=10)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;

            
            var now = DateTime.UtcNow;

         var allUserBlocks = await context.UserBlocks
        .Include(a => a.User)
        .Where(a => a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
        .OrderBy(a => a.StartsAt) // ترتيب مهم لتثبيت الصفحات
        .Skip((pageNumber - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

            
            return allUserBlocks.Select(b=> new AllBlocksDto
            {
                UserId = b.UserId,
                FirstName = b.User.FirstName,
                LastName = b.User.LastName,
                PhoneNumber = b.User.PhoneNumber,
                Reason = b.Reason,
                StartsAt = b.StartsAt,
                EndsAt = b.EndsAt

            }).ToList();
        }

        public async Task<bool> IsUserBlocked(string userId)
        {
            var now = DateTime.UtcNow;
            var isBlocked = await context.UserBlocks.FirstOrDefaultAsync(a=>a.UserId==userId && (a.EndsAt==null ||a.EndsAt>now ));
            if (isBlocked == null)
            {
                return false;
            }
            return true;
        }

        public async Task<bool?> ToggleUserBlock(string userId, string officeId, ToggleUserBlockDto dto)
        {
            var user = await context.Users.FindAsync(userId);
            if (user == null)
            {
                return null;
            }
            var now = DateTime.UtcNow;
            var isBlock = await context.UserBlocks.FirstOrDefaultAsync(a=>a.UserId==userId && (a.EndsAt==null || a.EndsAt > now));
            if (isBlock != null)// يعني اذا كان فيه حظر بدنا نشيلو
            {
                
                isBlock.EndsAt = now;
                await context.SaveChangesAsync();
                return false;
            }
            else
            {
                var userBlock = new UserBlock
                {
                    UserId = userId,
                    BlockedByUserId = officeId,
                    Reason = dto.Reason,
                    StartsAt = now,
                    EndsAt = dto.EndsAt
                };

                 context.UserBlocks.Add(userBlock);
                await context.SaveChangesAsync();
                return true;

            }
        }
    }
}
