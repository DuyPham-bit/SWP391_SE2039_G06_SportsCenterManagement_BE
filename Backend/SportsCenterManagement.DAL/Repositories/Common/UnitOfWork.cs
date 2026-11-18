using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore.Storage;
using SportsCenterManagement.DAL.Context;

namespace SportsCenterManagement.DAL.Repositories.Common;

public sealed class UnitOfWork(SportsCenterDbContext context) : IUnitOfWork
{
    private readonly ConcurrentDictionary<Type, object> _repositories = new();

    public SportsCenterDbContext Context => context;

    public IGenericRepository<T> Repository<T>() where T : class
    {
        return (IGenericRepository<T>)_repositories.GetOrAdd(
            typeof(T),
            _ => new GenericRepository<T>(context));
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return await context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await context.DisposeAsync();
    }
}
