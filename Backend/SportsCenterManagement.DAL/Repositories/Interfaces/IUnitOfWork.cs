using Microsoft.EntityFrameworkCore.Storage;
using SportsCenterManagement.DAL.Context;

namespace SportsCenterManagement.DAL.Repositories.Interfaces;

public interface IUnitOfWork : IAsyncDisposable
{
    SportsCenterDbContext Context { get; }
    IGenericRepository<T> Repository<T>() where T : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
