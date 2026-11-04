using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.WarehouseSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HRM.Application.Abstractions.Persistence.DevAndQA;

/// <summary>Persistence cho danh mục, template chung và snapshot từng phiếu IQC/COA.</summary>
public interface IEvaluationDbContext
{
    DbSet<AttachmentModel> AttachmentModels { get; }
    DbSet<MeasurementMethod> MeasurementMethods { get; }
    DbSet<EvaluationStandard> EvaluationStandards { get; }
    DbSet<EvaluationTemplate> EvaluationTemplates { get; }
    DbSet<EvaluationTemplateItem> EvaluationTemplateItems { get; }
    DbSet<IqcInspectionEvaluation> IqcInspectionEvaluations { get; }
    DbSet<IqcInspectionResult> IqcInspectionResults { get; }
    DbSet<CoaCertificate> CoaCertificates { get; }
    DbSet<CoaCertificateResult> CoaCertificateResults { get; }
    DbSet<QCInputByQC> QCInputByQCs { get; }
    DbSet<WarehouseVoucherDetail> WarehouseVoucherDetails { get; }
    DbSet<Employee> Employees { get; }
    DbSet<Product> Products { get; }
    Task<IDbContextTransaction> BeginIqcTransactionAsync(CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
