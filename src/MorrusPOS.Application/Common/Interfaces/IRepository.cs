using System.Linq.Expressions;

namespace MorrusPOS.Application.Common.Interfaces;

/// <summary>
/// Generic repository. Application layer hanya bergantung pada interface ini,
/// tidak tahu-menahu soal EF Core — supaya Infrastructure bisa diganti tanpa
/// menyentuh business logic.
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}

public interface IUnitOfWork
{
    IRepository<T> Repository<T>() where T : class;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
