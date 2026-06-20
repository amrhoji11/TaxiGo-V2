using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.DTO_S;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core.Interfaces
{
    public interface IComplaintRepository
    {
        Task<string> CreateComplaintAsync(string userId, int orderId, CreateComplaintDto dto);

        Task<List<ComplaintDto>> GetAllComplaintsAsync();

        Task<string> UpdateStatusAsync(int ComplaintId, UpdateComplaintStatusDto dto);

        Task<int> GetDriverViolationsCountAsync(string driverId);

        Task<List<ViolationDto>> GetAllViolationsAsync();
        Task<string> ResolveViolationAsync(int violationId);
            
            
    }
}
