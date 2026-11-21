using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.DevandqaSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HRM.Application.Abstractions.Persistence.DevAndQA;

/// <summary>Transaction cha cho lưu payload và file; use case con không tự commit transaction này.</summary>
public interface IEvaluationFileSaveDbContext : IEvaluationDbContext
{
    DbSet<AttachmentCollection> AttachmentCollections { get; }
    DbSet<LabTest> LabTests { get; }
    DbSet<LabTestResult> LabTestResults { get; }
    Task<IDbContextTransaction> BeginCombinedEvaluationSaveAsync(CancellationToken ct);
    void EndCombinedEvaluationSave();
}
