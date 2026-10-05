using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using HRM.Application.Abstractions.Persistence.PLM.ProductionOrders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace HRM.Application.Tests.Features.PLM.ProductionOrders;

// Không thay thế integration PostgreSQL: store này kiểm tra orchestration/rollback và
// cố ý không cho query nhìn thấy entity Added trước SaveChanges, như EF relational.
public class ProductionOrderTestStore : DispatchProxy
{
    private readonly Dictionary<string, object> _sets = [];
    public List<object> Added { get; } = [];
    public TestTransaction Transaction { get; } = new();
    public int Saves { get; private set; }
    public bool ThrowOnSave { get; set; }

    public static (IProductionOrderDbContext Db, ProductionOrderTestStore Store) Create()
    {
        var db = Create<IProductionOrderDbContext, ProductionOrderTestStore>();
        return (db, (ProductionOrderTestStore)db);
    }

    public void Seed<T>(string property, params T[] rows) where T : class
        => _sets[property] = new TestDbSet<T>(rows, Added);

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var method = targetMethod!;
        if (method.Name == nameof(IProductionOrderDbContext.BeginTransactionAsync))
            return Task.FromResult<IDbContextTransaction>(Transaction);
        if (method.Name == nameof(IProductionOrderDbContext.SaveChangesAsync))
        {
            Saves++;
            if (ThrowOnSave) throw new InvalidOperationException("Simulated write failure");
            return Task.FromResult(Added.Count);
        }
        if (method.Name.StartsWith("get_", StringComparison.Ordinal))
        {
            var property = method.Name[4..];
            if (!_sets.TryGetValue(property, out var set))
            {
                var entityType = method.ReturnType.GetGenericArguments()[0];
                set = Activator.CreateInstance(typeof(TestDbSet<>).MakeGenericType(entityType),
                    Array.CreateInstance(entityType, 0), Added)!;
                _sets[property] = set;
            }
            return set;
        }
        throw new NotSupportedException(method.Name);
    }

    public sealed class TestTransaction : IDbContextTransaction
    {
        public Guid TransactionId { get; } = Guid.NewGuid();
        public bool Committed { get; private set; }
        public bool RolledBack { get; private set; }
        public void Commit() => Committed = true;
        public Task CommitAsync(CancellationToken cancellationToken = default) { Commit(); return Task.CompletedTask; }
        public void Rollback() => RolledBack = true;
        public Task RollbackAsync(CancellationToken cancellationToken = default) { Rollback(); return Task.CompletedTask; }
        public void Dispose() { if (!Committed) Rollback(); }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }

    private sealed class TestDbSet<T>(T[] rows, List<object> added) : DbSet<T>, IQueryable<T>, IAsyncEnumerable<T>
        where T : class
    {
        private readonly AsyncQuery<T> _query = new(rows.AsQueryable().Expression);
        public override Microsoft.EntityFrameworkCore.Metadata.IEntityType EntityType => throw new NotSupportedException();
        public Type ElementType => typeof(T);
        public Expression Expression => ((IQueryable<T>)_query).Expression;
        public IQueryProvider Provider => ((IQueryable<T>)_query).Provider;
        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_query).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public override IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => _query.GetAsyncEnumerator(cancellationToken);
        public override ValueTask<EntityEntry<T>> AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            added.Add(entity);
            return ValueTask.FromResult<EntityEntry<T>>(null!);
        }
        public override EntityEntry<T> Add(T entity) { added.Add(entity); return null!; }
    }

    private sealed class AsyncQuery<T>(Expression expression) : EnumerableQuery<T>(expression), IAsyncEnumerable<T>, IQueryable<T>
    {
        IQueryProvider IQueryable.Provider => new AsyncProvider(this);
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => new AsyncEnumerator<T>(this.AsEnumerable().GetEnumerator(), cancellationToken);
    }

    private sealed class AsyncProvider(IQueryProvider inner) : IAsyncQueryProvider
    {
        public IQueryable CreateQuery(Expression expression)
            => (IQueryable)Activator.CreateInstance(typeof(AsyncQuery<>).MakeGenericType(
                expression.Type.GetGenericArguments()[0]), expression)!;
        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new AsyncQuery<TElement>(expression);
        public object? Execute(Expression expression) => inner.Execute(expression);
        public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);
        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resultType = typeof(TResult).GetGenericArguments()[0];
            var result = typeof(IQueryProvider).GetMethods()
                .Single(x => x.Name == nameof(Execute) && x.IsGenericMethod)
                .MakeGenericMethod(resultType).Invoke(inner, [expression]);
            return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType).Invoke(null, [result])!;
        }
    }

    private sealed class AsyncEnumerator<T>(IEnumerator<T> inner, CancellationToken ct) : IAsyncEnumerator<T>
    {
        public T Current => inner.Current;
        public ValueTask<bool> MoveNextAsync() { ct.ThrowIfCancellationRequested(); return ValueTask.FromResult(inner.MoveNext()); }
        public ValueTask DisposeAsync() { inner.Dispose(); return ValueTask.CompletedTask; }
    }
}
