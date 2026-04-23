using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.Interfaces
{
    public interface IEtaCacheService
    {
        bool TryGet(string key, out TimeSpan eta);
        void Set(string key, TimeSpan eta, int seconds);
    }
}
