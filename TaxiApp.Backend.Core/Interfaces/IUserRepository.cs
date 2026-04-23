using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.Interfaces
{
    public interface IUserRepository:IRepository<ApplicationUser>
    {
        Task<UsersResponseDto?> GetAllUsersAsync(int pageNumber = 1, int pageSize = 10);
        Task<UserListDto?> GetUserByIdAsync(string userId);
        Task<UsersResponseDto?> SearchUsersAsync(string? search, int? pageNumber = 1, int? pageSize = 10);
        Task<bool?> ToggleUserActive(string userId);
        Task<bool> IsUserActive(string userId);
        Task<bool> ChangeUserRole(string userId, string roleName);


    }
}
