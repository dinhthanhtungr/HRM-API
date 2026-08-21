using HRM.Domain.Entities.BomSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext
{
    public virtual DbSet<BomDefinition> BomDefinitions { get; set; } = default!;
    public virtual DbSet<BomVersion> BomVersions { get; set; } = default!;
    public virtual DbSet<BomVersionItem> BomVersionItems { get; set; } = default!;
    public virtual DbSet<ManufacturingBomStage> ManufacturingBomStages { get; set; } = default!;
    public virtual DbSet<ManufacturingLossType> ManufacturingLossTypes { get; set; } = default!;
    public virtual DbSet<ManufacturingBomLossRule> ManufacturingBomLossRules { get; set; } = default!;
    public virtual DbSet<ProductStandardBomVersion> ProductStandardBomVersions { get; set; } = default!;
}
