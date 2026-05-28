using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.StandardParameterSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.StandardParametersSchema
{
    public class MachineProductivityConfiguration : IEntityTypeConfiguration<MachineProductivity>
    {
        public void Configure(EntityTypeBuilder<MachineProductivity> entity)
        {
            entity.ToTable("machine_productivity", "standardparameters");

            entity.HasKey(x => x.Id)
                  .HasName("PK_standardparameters_machine_productivity");

            entity.Property(x => x.Id)
                  .HasColumnName("id")
                  .UseIdentityAlwaysColumn();

            entity.Property(x => x.MachineId)
                  .HasColumnName("machineid")
                  .HasColumnType("citext"); // nếu chưa bật extension, đổi về "text"

            entity.Property(x => x.ProductionCode)
                  .HasColumnName("productioncode")
                  .HasColumnType("citext"); // hoặc "text"

            entity.Property(x => x.Quantity)
                  .HasColumnName("quantity");

            // tra cứu thực dụng
            entity.HasIndex(x => new { x.MachineId, x.ProductionCode })
                  .HasDatabaseName("ix_machine_productivity_machineid_productioncode");
        }
    }
}
