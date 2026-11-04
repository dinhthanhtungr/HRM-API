using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Domain.Entities.DevandqaSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext : IIqcDbContext
{
    DbSet<EvaluationStandard> IIqcDbContext.EvaluationStandards => Set<EvaluationStandard>();

    public Task<IDbContextTransaction> BeginIqcTransactionAsync(CancellationToken cancellationToken = default) =>
        _combinedEvaluationSave && Database.CurrentTransaction is { } transaction
            ? Task.FromResult<IDbContextTransaction>(new BorrowedEvaluationTransaction(transaction))
            : Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
}
