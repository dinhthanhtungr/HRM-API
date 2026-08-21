using Microsoft.EntityFrameworkCore;
using HRM.Domain.Entities;
using HRM.Domain.Entities.ManufacturingSchema;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs
{
    public partial class ApplicationDbContext
    {
        public virtual DbSet<ColorChipManufacturingRecord> ColorChipManufacturingRecords { get; set; } = default!;
        public virtual DbSet<MfgProductionOrder> MfgProductionOrders { get; set; } = default!;
        public virtual DbSet<ManufacturingFormulaMaterial> ManufacturingFormulaMaterials { get; set; } = default!;
        public virtual DbSet<ManufacturingFormula> ManufacturingFormulas { get; set; } = default!;
        public virtual DbSet<ManufacturingFormulaVersion> ManufacturingFormulaVersions { get; set; } = default!;
        public virtual DbSet<ManufacturingFormulaVersionItem> ManufacturingFormulaVersionItems { get; set; } = default!;
        public virtual DbSet<MfgProductionOrderLoss> MfgProductionOrderLosses { get; set; } = default!;
        public virtual DbSet<ProductionSelectVersion> ProductionSelectVersions { get; set; } = default!;
        public virtual DbSet<ProductStandardFormula> ProductStandardFormulas { get; set; } = default!;
        public virtual DbSet<MfgOrderPO> MfgOrderPOs { get; set; } = default!;
        public virtual DbSet<SchedualMfg> SchedualMfgs { get; set; } = default!;
    }
}
