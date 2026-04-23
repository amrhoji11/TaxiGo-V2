using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Infrastructure.Data;

namespace TaxiApp.Backend.Infrastructure.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly ApplicationDbContext context;
        private readonly DbSet<T> dbSet;

        public Repository(ApplicationDbContext context)
        {
            this.context = context;
            this.dbSet = context.Set<T>();
        }
        public async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            await context.AddAsync(entity, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            return entity;

        }

        public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
        {
            return await context.SaveChangesAsync(cancellationToken);
        }

        public async Task<T?> Get(Expression<Func<T, bool>> expression, Expression<Func<T, object>>?[] includes = null, bool isTracked = true)
        {
           var all= await GetAll(expression, includes, isTracked);
            return all.FirstOrDefault();
        }

        public async Task<IEnumerable<T>> GetAll(Expression<Func<T, bool>>? expression = null, Expression<Func<T, object>>?[] includes = null, bool isTracked = true, int pageNumber = 1, int pageSize = 10)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;


            IQueryable<T> entity= dbSet;
            if (expression is not null)
            {
                entity = entity.Where(expression);

            }

            if (includes is not null)
            {
                foreach (var item in includes)
                {
                    entity=entity.Include(item);
                    
                }

            }

            if (!isTracked)
            {
                entity=entity.AsNoTracking();

            }

            return await entity.Skip((pageNumber - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync();
        }

        public async Task<bool> RemoveAsync(int id)
        {
            var entity = await dbSet.FindAsync(id);
            if (entity == null)
            {
                return false;
            }
             dbSet.Remove(entity);
            await context.SaveChangesAsync();
            return true;
        }
    }
}
