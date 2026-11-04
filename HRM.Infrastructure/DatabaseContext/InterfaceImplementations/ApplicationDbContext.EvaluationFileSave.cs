using System.Data;
using HRM.Application.Abstractions.Persistence.DevAndQA;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext : IEvaluationFileSaveDbContext
{
    private bool _combinedEvaluationSave;

    public async Task<IDbContextTransaction> BeginCombinedEvaluationSaveAsync(CancellationToken ct)
    {
        if (_combinedEvaluationSave || Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Không được lồng hai lần lưu evaluation kèm file.");
        var transaction = await Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        _combinedEvaluationSave = true;
        return transaction;
    }

    public void EndCombinedEvaluationSave() => _combinedEvaluationSave = false;

    // Chỉ dùng trong combined-save: quyền commit/rollback/dispose thuộc transaction cha.
    private sealed class BorrowedEvaluationTransaction(IDbContextTransaction transaction) : IDbContextTransaction
    {
        public Guid TransactionId => transaction.TransactionId;
        public void Commit() { }
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Rollback() { }
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
