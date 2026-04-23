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
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace TaxiApp.Backend.Infrastructure.Repositories
{
    public class UserRepository : Repository<ApplicationUser>, IUserRepository
    {
        private readonly ApplicationDbContext context;
        private readonly IUserBlockRepository userBlockRepository;
        private readonly UserManager<ApplicationUser> userManager;

        public UserRepository(ApplicationDbContext context, IUserBlockRepository userBlockRepository,UserManager<ApplicationUser> userManager) : base(context)
        {
            this.context = context;
            this.userBlockRepository = userBlockRepository;
            this.userManager = userManager;
        }

        public async Task<bool> ChangeUserRole(string userId, string roleName)
        {
            var user = await context.Users.FindAsync(userId);
            if (user != null)
            {
                var oldRole = await userManager.GetRolesAsync(user);
                if (oldRole.Any())
                {
                    var removeResult = await userManager.RemoveFromRolesAsync(user, oldRole);
                    if (!removeResult.Succeeded)
                        return false;
                }
               
                var result = await userManager.AddToRoleAsync(user, roleName);
                if (result.Succeeded)
                {
                    return true;
                }
            }

           

            return false;

        }

        public async Task<UsersResponseDto?> GetAllUsersAsync(int pageNumber=1 , int pageSize=10)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;

            var query = await context.Users.AsNoTracking().ToListAsync();
            if (query==null)
            {
                return null;
            }
            var totalCount =  query.Count();

            var users =  query.OrderBy(a=>a.FirstName).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
          
           // var UsersBlocke = await userBlockRepository.GetAllBlocks();

            var result = new List<UserListDto>();
            foreach (var user in users)
            {
                var roles = await userManager.GetRolesAsync(user);

                result.Add(new UserListDto
                {
                    UserId = user.Id,
                    FullName = user.FirstName + " " + user.LastName,
                    IsActive = user.IsActive,
                    IsBlocked = await userBlockRepository.IsUserBlocked(user.Id),
                    Role = roles.FirstOrDefault()
                });
                
            }

            return new UsersResponseDto
            {
                Count = totalCount,
                Users = result
            };

        }

        public async Task<UserListDto?> GetUserByIdAsync(string userId)
        {
            var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(a => a.Id == userId);
            if (user == null)
            {
                return null;
            }
            var isBlock = await userBlockRepository.IsUserBlocked(userId);
            var role = await userManager.GetRolesAsync(user);
            return new UserListDto
            {
                UserId = user.Id,
                FullName = user.FirstName + " " + user.LastName,
                IsActive = user.IsActive,
                IsBlocked = isBlock,
                IsDeleted = user.IsDeleted,
                Role = role.FirstOrDefault()
            };
            
        }

        public async Task<bool> IsUserActive(string userId)
        {
            var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(a=>a.Id == userId);
            if (user == null)
            {
                return false;
            }
            return user.IsActive;
        }

        public async Task<UsersResponseDto?> SearchUsersAsync(string? search , int? pageNumber = 1, int? pageSize = 10)
        {
            int currentPage = pageNumber ?? 1;
            int currentSize = pageSize ?? 10;

            if (currentPage < 1) currentPage = 1;
            if (currentSize < 1) currentSize = 10;

            var query = context.Users
                               .AsNoTracking()
                               .AsQueryable();

            // 🔍 Search
            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"{search}%";

                query = query.Where(u =>
                    EF.Functions.Like(u.PhoneNumber, pattern) ||
                    EF.Functions.Like(u.UserName, pattern) ||
                    EF.Functions.Like(u.FirstName, pattern) ||
                    EF.Functions.Like(u.LastName, pattern)
                );
            }

            // 📊 Total Count
            var totalCount = await query.CountAsync();

            // 📄 Pagination
            var users = await query
                .OrderBy(u => u.FirstName)
                .Skip((currentPage - 1) * currentSize)
                .Take(currentSize)
                .ToListAsync();   // 🔥 مهم جدًا لحل مشكلة DataReader

            var result = new List<UserListDto>();

            foreach (var user in users)
            {
                var roles = await userManager.GetRolesAsync(user);

                result.Add(new UserListDto
                {
                    UserId = user.Id,
                    FullName = $"{user.FirstName} {user.LastName}",
                    IsActive = user.IsActive,
                    IsBlocked = await userBlockRepository.IsUserBlocked(user.Id),
                    Role = roles.FirstOrDefault()
                });
            }

            return new UsersResponseDto
            {
                Count = totalCount,
                Users = result
            };


        }

        

        public async Task<bool?> ToggleUserActive(string userId)
        {
            var user = await context.Users.FindAsync(userId);
            if (user == null)
            {
                return null;
            }
          user.IsActive = !user.IsActive;

            await context.SaveChangesAsync();
            return user.IsActive;
        }
    }
}
