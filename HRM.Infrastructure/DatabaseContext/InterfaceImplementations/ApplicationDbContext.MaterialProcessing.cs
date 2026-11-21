using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Domain.Entities.MaterialSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext : IMaterialProcessingDbContext
{
    public DbSet<MaterialProcessingFormula> MaterialProcessingFormulas { get; set; } = default!;
    public DbSet<MaterialProcessingFormulaDetail> MaterialProcessingFormulaDetails { get; set; } = default!;
}
