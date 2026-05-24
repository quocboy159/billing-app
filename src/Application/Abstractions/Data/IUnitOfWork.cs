using Microsoft.EntityFrameworkCore.Storage;

namespace Application.Abstractions.Data;

/// <summary>
/// Coordinates persistence across one or more repositories. All repositories share the
/// same underlying scoped DbContext, so a single <see cref="SaveChangesAsync"/> commits
/// every tracked change.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
