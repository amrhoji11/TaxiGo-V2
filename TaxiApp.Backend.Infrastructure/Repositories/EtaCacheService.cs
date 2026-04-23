using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.Interfaces;

namespace TaxiApp.Backend.Infrastructure.Repositories
{
    public class EtaCacheService : IEtaCacheService
    {
        private readonly IMemoryCache _cache;

        public EtaCacheService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public bool TryGet(string key, out TimeSpan eta)
        {
            return _cache.TryGetValue(key, out eta);
        }

        public void Set(string key, TimeSpan eta, int seconds)
        {
            _cache.Set(key, eta, TimeSpan.FromSeconds(seconds));
        }
    }
}
