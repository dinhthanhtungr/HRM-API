using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

/// <summary>Persist staged quotation changes exactly once; failures discard all tracked changes.</summary>
internal sealed class QuotationAtomicSaveService(ICRMWriteDbContext dbContext)
{
    public async Task<OperationResult<QuotationTotalsDto>> ExecuteAsync(
        Func<Task<OperationResult<QuotationTotalsDto>>> stage,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.BeginQuotationTransactionAsync(cancellationToken);
        var committed = false;
        try
        {
            var result = await stage();
            if (!result.Success) return result;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;
            return result;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            return OperationResult<QuotationTotalsDto>.Fail(
                OptimisticConcurrencyHelper.CreateConflictMessage("Quotation", exception));
        }
        finally
        {
            if (!committed)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                finally
                {
                    dbContext.ClearTrackedChanges();
                }
            }
        }
    }
}
