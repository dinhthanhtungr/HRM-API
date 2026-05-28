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
    public class ParameterStandardConfiguration : IEntityTypeConfiguration<ParameterStandard>
    {
        public void Configure(EntityTypeBuilder<ParameterStandard> entity)
        {
            entity.ToTable("parameterstandard", "standardparameters");

            // Bảng không có cột id -> dùng PK tổng hợp theo logic nghiệp vụ
            entity.HasKey(x => new { x.MachineId, x.ProductionCode })
                  .HasName("PK_standardparameters_parameterstandard");

            entity.Property(x => x.MachineId)
                  .HasColumnName("machineid")
                  .HasColumnType("citext"); // hoặc "text"

            entity.Property(x => x.ProductionCode)
                  .HasColumnName("productioncode")
                  .HasColumnType("citext"); // hoặc "text"

            entity.Property(x => x.Set1Standard).HasColumnName("set1_standard");
            entity.Property(x => x.Set2Standard).HasColumnName("set2_standard");
            entity.Property(x => x.Set3Standard).HasColumnName("set3_standard");
            entity.Property(x => x.Set4Standard).HasColumnName("set4_standard");
            entity.Property(x => x.Set5Standard).HasColumnName("set5_standard");
            entity.Property(x => x.Set6Standard).HasColumnName("set6_standard");
            entity.Property(x => x.Set7Standard).HasColumnName("set7_standard");
            entity.Property(x => x.Set8Standard).HasColumnName("set8_standard");
            entity.Property(x => x.Set9Standard).HasColumnName("set9_standard");
            entity.Property(x => x.Set10Standard).HasColumnName("set10_standard");
            entity.Property(x => x.Set11Standard).HasColumnName("set11_standard");
            entity.Property(x => x.Set12Standard).HasColumnName("set12_standard");
            entity.Property(x => x.Set13Standard).HasColumnName("set13_standard");

            entity.Property(x => x.ScrewSpeedStandard)
                  .HasColumnName("screwspeed_standard");

            entity.Property(x => x.ScrewCurrentStandard)
                  .HasColumnName("screwcurrent_standard");

            entity.Property(x => x.FeederSpeedStandard)
                  .HasColumnName("feederspeed_standard");

            entity.Property(x => x.EmployeeId)
                  .HasColumnName("employeeid");
        }
    }
}
