using HRM.Domain.Entities.DevandqaSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext
{
    public DbSet<IqcSpecification> IqcSpecifications { get; set; } = default!;
    public DbSet<IqcSpecificationItem> IqcSpecificationItems { get; set; } = default!;
    public DbSet<IqcInspectionEvaluation> IqcInspectionEvaluations { get; set; } = default!;
    public DbSet<IqcInspectionResult> IqcInspectionResults { get; set; } = default!;
}
