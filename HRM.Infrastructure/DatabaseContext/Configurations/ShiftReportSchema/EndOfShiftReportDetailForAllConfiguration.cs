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
    public class EndOfShiftReportDetailForAllConfiguration : IEntityTypeConfiguration<EndOfShiftReportDetailForAll>
    {
        public void Configure(EntityTypeBuilder<EndOfShiftReportDetailForAll> entity)
        {
            entity.ToTable("endofshiftreportdetailforall", "shiftreports");

            entity.HasKey(x => x.ShiftReportDetailForAllId);

            entity.Property(x => x.ShiftReportDetailForAllId)
                  .UseIdentityAlwaysColumn()
                  .HasColumnName("shiftreportdetailforall_id");

            entity.Property(x => x.ExternalId)
                  .HasColumnName("externalid")
                  .HasColumnType("citext");

            entity.Property(x => x.ProductType)
                  .HasColumnName("producttype");

            entity.Property(x => x.NetWeight)
                  .HasColumnName("netweight")
                  .HasPrecision(16, 3);

            entity.Property(x => x.WeightStockedKg)
                  .HasColumnName("weightstockedkg")
                  .HasPrecision(16, 3);

            entity.Property(x => x.ShiftReportForAllId)
                  .HasColumnName("shiftreportforall_id");

            entity.HasIndex(x => x.ExternalId)
                  .HasDatabaseName("ix_eosrdf_externalid");

            entity.HasOne(x => x.ShiftReportForAll)
                  .WithMany(x => x.ShiftReportDetails)
                  .HasForeignKey(x => x.ShiftReportForAllId)
                  .HasConstraintName("fk_eosrdf_eosrf");
        }
    }
}
