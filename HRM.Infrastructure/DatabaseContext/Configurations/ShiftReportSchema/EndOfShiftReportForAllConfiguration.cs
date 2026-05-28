using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.ShiftReportSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.ShiftReportSchema
{
    public class EndOfShiftReportForAllConfiguration : IEntityTypeConfiguration<EndOfShiftReportForAll>
    {
        public void Configure(EntityTypeBuilder<EndOfShiftReportForAll> entity)
        {
            entity.ToTable("endofshiftreportforall", "shiftreports");

            entity.HasKey(x => x.ShiftReportForAllId);

            entity.Property(x => x.ShiftReportForAllId)
                  .UseIdentityAlwaysColumn()
                  .HasColumnName("shiftreportforall_id");

            entity.Property(x => x.CreatedAt)
                  .HasColumnName("created_at");

            entity.Property(x => x.ShiftId)
                  .HasColumnName("shiftid");

            entity.Property(x => x.MachineId)
                  .HasColumnName("machineid");

            entity.Property(x => x.Operator)
                  .HasColumnName("operator");

            entity.Property(x => x.ProductCode)
                  .HasColumnName("productcode")
                  .HasColumnType("citext");

            entity.Property(x => x.ExternalId)
                  .HasColumnName("externalid")
                  .HasColumnType("citext");

            entity.Property(x => x.Note)
                  .HasColumnName("note");

            entity.Property(x => x.ProductStatus)
                  .HasColumnName("productstatus");

            entity.HasIndex(x => x.CreatedAt)
                  .HasDatabaseName("ix_eosrf_created_at");

            entity.HasIndex(x => new { x.MachineId, x.ShiftId })
                  .HasDatabaseName("ix_eosrf_machine_shift");

            entity.HasIndex(x => x.ProductCode)
                  .HasDatabaseName("ix_eosrf_productcode");
        }
    }
}
