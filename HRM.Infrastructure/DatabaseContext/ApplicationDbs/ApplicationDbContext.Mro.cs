using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.MROSchema;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs
{
    public partial class ApplicationDbContext
    {
        public virtual DbSet<AreaMRO> AreasMro { get; set; } = default!;
        public virtual DbSet<WarehouseMRO> WarehousesMro { get; set; } = default!;
        public virtual DbSet<ZoneMRO> ZonesMro { get; set; } = default!;
        public virtual DbSet<RackMRO> RacksMro { get; set; } = default!;
        public virtual DbSet<SlotMRO> SlotsMro { get; set; } = default!;
        public virtual DbSet<EquipmentMRO> EquipmentsMro { get; set; } = default!;
        public virtual DbSet<EquipmentTypeMRO> EquipmentTypesMro { get; set; } = default!;
        public virtual DbSet<EquipmentDetailMRO> EquipmentDetailsMro { get; set; } = default!;
        public virtual DbSet<EquipmentSpecMRO> EquipmentSpecsMro { get; set; } = default!;
        public virtual DbSet<IncidentHeaderMRO> IncidentHeadersMro { get; set; } = default!;
        public virtual DbSet<IncidentLineMRO> IncidentLinesMro { get; set; } = default!;
        public virtual DbSet<StockOutHeaderMRO> StockOutHeadersMro { get; set; } = default!;
        public virtual DbSet<StockOutLineMRO> StockOutLinesMro { get; set; } = default!;
        public virtual DbSet<ImprovementHdrMRO> ImprovementHeadersMro { get; set; } = default!;
        public virtual DbSet<ImprovementHistoryMRO> ImprovementHistoriesMro { get; set; } = default!;
        public virtual DbSet<PmPlanMRO> PmPlansMro { get; set; } = default!;
        public virtual DbSet<PmPlanHistoryMRO> PmPlanHistoriesMro { get; set; } = default!;
        public virtual DbSet<MovementMRO> MovementsMro { get; set; } = default!;
        public virtual DbSet<TransferHeaderMRO> TransferHeadersMro { get; set; } = default!;
        public virtual DbSet<TransferDetailMRO> TransferDetailsMro { get; set; } = default!;

    }
}
