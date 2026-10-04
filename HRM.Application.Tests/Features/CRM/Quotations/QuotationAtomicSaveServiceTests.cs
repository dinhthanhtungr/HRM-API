using System.Reflection;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class QuotationAtomicSaveServiceTests
{
    [Fact]
    public async Task LaterValidationFailureNeverPersistsStagedHeader()
    {
        var (service, context) = Create();
        var result = await service.ExecuteAsync(() =>
        {
            context.Events.Add("stage-header");
            return Task.FromResult(OperationResult<QuotationTotalsDto>.Fail("invalid line"));
        }, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(["begin", "stage-header", "rollback", "clear", "dispose"], context.Events);
    }

    [Fact]
    public async Task AllSectionsValidateBeforeSingleSaveAndCommit()
    {
        var (service, context) = Create();
        var result = await service.ExecuteAsync(() =>
        {
            context.Events.Add("stage-header");
            context.Events.Add("stage-lines");
            return Task.FromResult(OperationResult<QuotationTotalsDto>.Ok(new QuotationTotalsDto()));
        }, CancellationToken.None);
        Assert.True(result.Success);
        Assert.Equal(["begin", "stage-header", "stage-lines", "save", "commit", "dispose"], context.Events);
    }

    [Fact]
    public async Task DatabaseConcurrencyFailureRollsBackAndDiscardsTracking()
    {
        var (service, context) = Create();
        context.SaveException = new DbUpdateConcurrencyException();
        var result = await service.ExecuteAsync(
            () => Task.FromResult(OperationResult<QuotationTotalsDto>.Ok(new QuotationTotalsDto())),
            CancellationToken.None);
        Assert.False(result.Success);
        Assert.True(OptimisticConcurrencyHelper.IsConflictMessage(result.Message));
        Assert.Equal(["begin", "save", "rollback", "clear", "dispose"], context.Events);
    }

    [Fact]
    public async Task UnexpectedSaveFailureAlsoRollsBack()
    {
        var (service, context) = Create();
        context.SaveException = new InvalidOperationException("database failure");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(
            () => Task.FromResult(OperationResult<QuotationTotalsDto>.Ok(new QuotationTotalsDto())),
            CancellationToken.None));
        Assert.Equal(["begin", "save", "rollback", "clear", "dispose"], context.Events);
    }

    [Fact]
    public async Task StagingExceptionDiscardsAllChangesWithoutSaving()
    {
        var (service, context) = Create();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(
            () => throw new InvalidOperationException("line validation failed unexpectedly"),
            CancellationToken.None));
        Assert.Equal(["begin", "rollback", "clear", "dispose"], context.Events);
    }

    private static (QuotationAtomicSaveService Service, ContextProxy Context) Create()
    {
        var db = DispatchProxy.Create<ICRMWriteDbContext, ContextProxy>();
        return (new QuotationAtomicSaveService(db), (ContextProxy)(object)db);
    }

    public class ContextProxy : DispatchProxy
    {
        public List<string> Events { get; } = [];
        public Exception? SaveException { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method!.Name)
            {
                case nameof(ICRMWriteDbContext.BeginQuotationTransactionAsync):
                    Events.Add("begin");
                    return Task.FromResult<IDbContextTransaction>(new Transaction(Events));
                case nameof(ICRMWriteDbContext.SaveChangesAsync):
                    Events.Add("save");
                    return SaveException is null ? Task.FromResult(1) : Task.FromException<int>(SaveException);
                case nameof(ICRMWriteDbContext.ClearTrackedChanges):
                    Events.Add("clear");
                    return null;
                default: throw new NotSupportedException(method.Name);
            }
        }
    }

    private sealed class Transaction(List<string> events) : IDbContextTransaction
    {
        public Guid TransactionId { get; } = Guid.NewGuid();
        public void Commit() => events.Add("commit");
        public Task CommitAsync(CancellationToken cancellationToken = default) { Commit(); return Task.CompletedTask; }
        public void Rollback() => events.Add("rollback");
        public Task RollbackAsync(CancellationToken cancellationToken = default) { Rollback(); return Task.CompletedTask; }
        public void Dispose() => events.Add("dispose");
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
