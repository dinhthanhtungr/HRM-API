using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.PrintectSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.PrintectSchema
{
    public class HistoryPrintLabelForAllConfiguration : IEntityTypeConfiguration<HistoryPrintLabelForAll>
    {
        public void Configure(EntityTypeBuilder<HistoryPrintLabelForAll> entity)
        {
            entity.ToTable("history_print_label_for_all", "printect");
            entity.HasNoKey();
            entity.Property(x => x.Id)
             .HasColumnName("historyprintlabelforall_id")
             .UseIdentityAlwaysColumn();

            entity.Property(x => x.ExternalId)
             .HasColumnName("externalid")
             .HasColumnType("citext");

            entity.Property(x => x.NumberOfCopies)
             .HasColumnName("numberofcopies");

            entity.Property(x => x.CreatedAt)
             .HasColumnName("created_at");

            entity.Property(x => x.ShiftId)
             .HasColumnName("shiftid");

            entity.Property(x => x.LogNumber)
             .HasColumnName("lognumber");

            entity.Property(x => x.ProductionDate)
             .HasColumnName("productiondate");
            // Index thực dụng cho log
            entity.HasIndex(x => x.CreatedAt)
             .HasDatabaseName("ix_hpla_created_at");

            entity.HasIndex(x => new { x.LogNumber, x.CreatedAt })
             .HasDatabaseName("ix_hpla_lognumber_created_at");

            entity.HasIndex(x => new { x.ExternalId, x.CreatedAt })
             .HasDatabaseName("ix_hpla_externalid_created_at");
        }
    }
}
