using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace NLightning.Infrastructure.Repositories.Database;

using Helpers;
using Persistence.Contexts;

public class BaseDbRepository<TEntity> where TEntity : class
{
    private readonly NLightningDbContext _context;
    protected readonly DbSet<TEntity> DbSet;

    protected BaseDbRepository(NLightningDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        DbSet = context.Set<TEntity>();
    }

    protected IQueryable<TEntity> Get(Expression<Func<TEntity, bool>>? predicate = null,
                                      Expression<Func<TEntity, object>>? include = null,
                                      Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
                                      bool asNoTracking = true, int perPage = 0, int pageNumber = 1)
    {
        var query = asNoTracking ? DbSet.AsNoTracking() : DbSet;

        if (predicate is not null)
            query = query.Where(predicate);

        if (include is not null)
            query = query.Include(include);

        if (perPage > 0)
            query = query.Skip((pageNumber - 1) * perPage).Take(perPage);

        return orderBy is not null ? orderBy(query) : query;
    }

    protected async Task<TEntity?> GetByIdAsync(object id, bool asNoTracking = true,
                                                Expression<Func<TEntity, object>>? include = null)
    {
        var query = asNoTracking ? DbSet.AsNoTracking() : DbSet;

        if (include is not null)
            query = query.Include(include);

        var lambdaPredicate = PrimaryKeyHelper.GetPrimaryKeyExpression<TEntity>(id, _context)
                           ?? throw new InvalidOperationException(
                                  $"Entity {typeof(TEntity).Name} does not have a primary key defined.");

        query = query.Where(lambdaPredicate);

        return await query.FirstOrDefaultAsync();
    }

    protected void Insert(TEntity entity)
    {
        DbSet.Add(entity);
    }

    protected void Delete(TEntity entityToDelete)
    {
        if (_context.Entry(entityToDelete).State == EntityState.Detached)
            DbSet.Attach(entityToDelete);

        DbSet.Remove(entityToDelete);
    }

    protected async Task DeleteByIdAsync(object id)
    {
        var entityToDelete = await GetByIdAsync(id, false)
                          ?? throw new InvalidOperationException($"Entity with id {id} not found.");

        Delete(entityToDelete);
    }

    protected void DeleteRange(IEnumerable<TEntity> entitiesToDelete)
    {
        var iEnumerable = entitiesToDelete as TEntity[] ?? entitiesToDelete.ToArray();
        if (iEnumerable.Length == 0)
            return;

        foreach (var entity in iEnumerable)
        {
            if (_context.Entry(entity).State == EntityState.Detached)
                DbSet.Attach(entity);
        }

        DbSet.RemoveRange(iEnumerable);
    }

    protected void DeleteWhere(Expression<Func<TEntity, bool>> predicate)
    {
        var entitiesToDelete = DbSet.Where(predicate).ToArray();
        if (entitiesToDelete.Length == 0)
            return;

        DeleteRange(entitiesToDelete);
    }

    protected void Update(TEntity entityToUpdate)
    {
        // Get the primary key value
        var keyValues = _context.Entry(entityToUpdate).Metadata.FindPrimaryKey()?.Properties
            .Select(p => _context.Entry(entityToUpdate).Property(p.Name).CurrentValue).ToArray();

        if (keyValues == null || keyValues.Length == 0)
        {
            DbSet.Update(entityToUpdate);
            return;
        }

        // Find tracked entity by primary key
        var trackedEntity = DbSet.Local.FirstOrDefault(e =>
        {
            var trackedKeyValues = _context.Entry(e).Metadata.FindPrimaryKey()?.Properties
                .Select(p => _context.Entry(e).Property(p.Name).CurrentValue).ToArray();
            return trackedKeyValues != null && keyValues.SequenceEqual(trackedKeyValues);
        });

        if (trackedEntity is not null)
        {
            // If the entity is already tracked, update its values
            var entry = _context.Entry(trackedEntity);
            entry.CurrentValues.SetValues(entityToUpdate);
        }
        else
        {
            // If the entity is not tracked, attach it and set its state to modified
            DbSet.Update(entityToUpdate);
        }
    }
}