using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.Interfaces
{
    public interface ISettingsRepository
    {
        Task<bool> UpdateLanguageAsync(string userId, string language);
        Task<string?> GetLanguageAsync(string userId);
        Task<bool> UpdateNotificationsAsync(string userId, bool enabled);
        Task<bool?> GetNotificationsStatusAsync(string userId);
        Task<bool> UpdateDarkModeAsync(string userId, bool enabled);
        Task<bool?> GetDarkModeAsync(string userId);

       
    }
}
