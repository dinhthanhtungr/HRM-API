using Microsoft.EntityFrameworkCore;
using VietausWebAPI.Core.Domain.Entities.DevandqaSchema;

namespace VietausWebAPI.Infrastructure.DatabaseContext.ApplicationDbs;

/// <summary>DbSet cho danh mục, template và dữ liệu IQC/COA động; không thay đổi các DbSet legacy.</summary>
public partial class ApplicationDbContext
{
    public DbSet<MeasurementMethod> MeasurementMethods { get; set; } = default!;
    public DbSet<EvaluationStandard> EvaluationStandards { get; set; } = default!;
    public DbSet<EvaluationTemplate> EvaluationTemplates { get; set; } = default!;
    public DbSet<EvaluationTemplateItem> EvaluationTemplateItems { get; set; } = default!;
    public DbSet<IqcSpecification> IqcSpecifications { get; set; } = default!;
    public DbSet<IqcSpecificationItem> IqcSpecificationItems { get; set; } = default!;
    public DbSet<IqcInspectionEvaluation> IqcInspectionEvaluations { get; set; } = default!;
    public DbSet<IqcInspectionResult> IqcInspectionResults { get; set; } = default!;
    public DbSet<CoaSpecification> CoaSpecifications { get; set; } = default!;
    public DbSet<CoaSpecificationItem> CoaSpecificationItems { get; set; } = default!;
    public DbSet<CoaCertificate> CoaCertificates { get; set; } = default!;
    public DbSet<CoaCertificateResult> CoaCertificateResults { get; set; } = default!;
}
