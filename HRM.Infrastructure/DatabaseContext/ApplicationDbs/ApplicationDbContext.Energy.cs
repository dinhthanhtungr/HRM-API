using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities;
using HRM.Domain.Entities.EnergyScheme;
using HRM.Domain.Entities.MaterialSchema;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs
{
    public partial class ApplicationDbContext
    {
        public virtual DbSet<EnergyGroupTariffMap> EnergyGroupTariffMaps { get; set; } = default!;
        public virtual DbSet<EnergyTouCalendar> EnergyTouCalendars { get; set; } = default!;
        public virtual DbSet<EnergyTouException> EnergyTouExceptions { get; set; } = default!;
        public virtual DbSet<EnergyGroup> EnergyGroups { get; set; } = default!;
        public virtual DbSet<EnergyMeter> EnergyMeters { get; set; } = default!;
        public virtual DbSet<EnergyTariff> EnergyTariffs { get; set; } = default!;
        public virtual DbSet<EnergyTariffVersion> EnergyTariffVersions { get; set; } = default!;
        public virtual DbSet<EnergyTariffBandRate> EnergyTariffBandRates { get; set; } = default!;
        public virtual DbSet<EnergyRegisterSnapshot> EnergyRegisterSnapshots { get; set; } = default!;
        public virtual DbSet<EnergyMeterGroupHistory> EnergyMeterGroupHistories { get; set; } = default!;
        public virtual DbSet<EnergyMeterCommConfig> EnergyMeterCommConfigs { get; set; } = default!;
        public virtual DbSet<EnergyTouWindow> EnergyTouWindows { get; set; } = default!;
        public virtual DbSet<EnergyReadingsHourly> EnergyReadingsHourlies { get; set; } = default!;
        public virtual DbSet<EnergyReadingsHourlyVn> EnergyReadingsHourlyVns { get; set; } = default!;
    }
}
