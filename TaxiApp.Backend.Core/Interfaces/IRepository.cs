using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.Interfaces
{
    public interface IRepository<T> where T : class
    {
        Task<IEnumerable<T>> GetAll(Expression<Func<T,bool>>? expression = null, Expression<Func<T, object>>?[] includes= null , bool isTracked= true, int pageNumber = 1, int pageSize = 10);
        Task<T?> Get(Expression<Func<T,bool>> expression , Expression<Func<T, object>>?[] includes = null , bool isTracked = true);
        Task<T> AddAsync(T entity , CancellationToken cancellationToken =default);
        Task<int> CommitAsync(CancellationToken cancellationToken = default);
        Task<bool> RemoveAsync(int id);



    }
}
