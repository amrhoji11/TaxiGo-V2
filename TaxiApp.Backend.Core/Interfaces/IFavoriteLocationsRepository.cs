using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.Interfaces
{
    public interface IFavoriteLocationsRepository
    {
        Task AddLocationAsync(string userId, AddFavoriteLocationDto dto);
        Task<List<FavoriteLocation>> GetLocationsAsync(string userId);
        Task<bool> DeleteLocationAsync(string userId, int locationId);
    }
}
