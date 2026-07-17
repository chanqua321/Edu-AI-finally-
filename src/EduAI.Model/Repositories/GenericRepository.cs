using System.Linq.Expressions;
using EduAI.Model.IRepository;
using Microsoft.EntityFrameworkCore;

namespace EduAI.Model.Repositories;

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly AppDbContext Context;
    protected readonly DbSet<T> DbSet;

    public GenericRepository(AppDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(int id) =>
        await DbSet.FindAsync(id);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync() =>
        await DbSet.AsNoTracking().ToListAsync();

    public virtual async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate) =>
        await DbSet.AsNoTracking().Where(predicate).ToListAsync();

    public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate) =>
        await DbSet.FirstOrDefaultAsync(predicate);

    public virtual async Task AddAsync(T entity) =>
        await DbSet.AddAsync(entity);

    public virtual void Update(T entity)
    {
        var entry = Context.Entry(entity);
        var primaryKey = entry.Metadata.FindPrimaryKey();
        if (primaryKey != null)
        {
            var keyValues = primaryKey.Properties
                .Select(p => entry.Property(p.Name).CurrentValue)
                .ToArray();

            var local = DbSet.Local.FirstOrDefault(localEntity =>
            {
                var localEntry = Context.Entry(localEntity);
                var localKeyValues = primaryKey.Properties
                    .Select(p => localEntry.Property(p.Name).CurrentValue)
                    .ToArray();
                return keyValues.SequenceEqual(localKeyValues);
            });

            if (local != null)
            {
                Context.Entry(local).State = EntityState.Detached;
            }
        }

        DbSet.Update(entity);
    }

    public virtual void Remove(T entity) => DbSet.Remove(entity);
}
