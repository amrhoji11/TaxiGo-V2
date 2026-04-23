using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.Interfaces
{
    public interface IFileService
    {
        Task<string?> SaveImageAsync(IFormFile img, string folderName);

    }
}
