using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.Interfaces
{
    public interface IUserBlockRepository
    {
        Task<bool?> ToggleUserBlock(string userId,string officeId,ToggleUserBlockDto dto);
        Task<IEnumerable<AllBlocksDto?>> GetAllBlocks(int pageNumber = 1, int pageSize = 10);
        Task<bool> IsUserBlocked(string userId);
    }
}
