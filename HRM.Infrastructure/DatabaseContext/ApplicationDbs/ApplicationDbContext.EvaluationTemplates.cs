using HRM.Domain.Entities.DevandqaSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext
{
    public DbSet<MeasurementMethod> MeasurementMethods { get; set; } = default!;
    public DbSet<EvaluationTemplate> EvaluationTemplates { get; set; } = default!;
    public DbSet<EvaluationTemplateItem> EvaluationTemplateItems { get; set; } = default!;
}
