using HRM.Domain.Entities;
using HRM.Domain.Entities.EnergyScheme;
using HRM.Domain.Entities.MaterialSchema;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs
{
    public partial class ApplicationDbContext
    {
        public virtual DbSet<Group> Group { get; set; }
        public virtual DbSet<GroupTariffMap> GroupTariffMaps { get; set; }

        public virtual DbSet<Meter> Meters { get; set; }
        public virtual DbSet<MeterGroupHistory> MeterGroupHistories { get; set; }

        public virtual DbSet<ReadingsHourly> ReadingsHourlies { get; set; }

        public virtual DbSet<ReadingsHourlyVn> ReadingsHourlyVns { get; set; }

        public virtual DbSet<RegisterSnapshot> RegisterSnapshots { get; set; }

        public virtual DbSet<Tariff> Tariffs { get; set; }

        public virtual DbSet<TariffBandRate> TariffBandRates { get; set; }

        public virtual DbSet<TariffVersion> TariffVersions { get; set; }

        public virtual DbSet<TouCalendar> TouCalendars { get; set; }

        public virtual DbSet<TouException> TouExceptions { get; set; }

        public virtual DbSet<TouWindow> TouWindows { get; set; }
    }
}
