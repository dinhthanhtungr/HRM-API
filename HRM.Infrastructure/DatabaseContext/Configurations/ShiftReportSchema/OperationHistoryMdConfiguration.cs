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
    public class OperationHistoryMdConfiguration : IEntityTypeConfiguration<OperationHistoryMd>
    {
        public void Configure(EntityTypeBuilder<OperationHistoryMd> entity)
        {
            entity.ToTable("OperationHistory_MD", "shiftreports");
            // Index gợi ý cho truy vấn theo thời gian & mã hàng
            entity.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_op_history_created_at");
            entity.HasIndex(x => new { x.ProductCode, x.CreatedAt }).HasDatabaseName("ix_op_history_prod_created_at");
            entity.HasNoKey();
            // Cột thời gian: lưu UTC ở DB
            entity.Property(x => x.CreatedAt)
                  .HasColumnName("created_at");

            entity.Property(x => x.ProductCode)
                  .HasColumnName("productcode")
                  .HasColumnType("text");

            entity.Property(x => x.ExternalId)
                  .HasColumnName("externalid")
                  .HasColumnType("text");

            // các trường set/act
            entity.Property(x => x.Set1).HasColumnName("set1");
            entity.Property(x => x.Act1).HasColumnName("act1");
            entity.Property(x => x.Set2).HasColumnName("set2");
            entity.Property(x => x.Act2).HasColumnName("act2");
            entity.Property(x => x.Set3).HasColumnName("set3");
            entity.Property(x => x.Act3).HasColumnName("act3");
            entity.Property(x => x.Set4).HasColumnName("set4");
            entity.Property(x => x.Act4).HasColumnName("act4");
            entity.Property(x => x.Set5).HasColumnName("set5");
            entity.Property(x => x.Act5).HasColumnName("act5");
            entity.Property(x => x.Set6).HasColumnName("set6");
            entity.Property(x => x.Act6).HasColumnName("act6");
            entity.Property(x => x.Set7).HasColumnName("set7");
            entity.Property(x => x.Act7).HasColumnName("act7");
            entity.Property(x => x.Set8).HasColumnName("set8");
            entity.Property(x => x.Act8).HasColumnName("act8");
            entity.Property(x => x.Set9).HasColumnName("set9");
            entity.Property(x => x.Act9).HasColumnName("act9");
            entity.Property(x => x.Set10).HasColumnName("set10");
            entity.Property(x => x.Act10).HasColumnName("act10");
            entity.Property(x => x.Set11).HasColumnName("set11");
            entity.Property(x => x.Act11).HasColumnName("act11");
            entity.Property(x => x.Set12).HasColumnName("set12");
            entity.Property(x => x.Act12).HasColumnName("act12");
            entity.Property(x => x.Set13).HasColumnName("set13");
            entity.Property(x => x.Act13).HasColumnName("act13");

            // thông số tốc độ/dòng
            entity.Property(x => x.ScrewSpeed).HasColumnName("screwspeed");
            entity.Property(x => x.ScrewCurrent).HasColumnName("screwcurrent");
            entity.Property(x => x.FeederSpeed).HasColumnName("feederspeed");

            entity.Property(x => x.MachineId)
                .HasColumnName("machine_id");

            entity.HasIndex(x => x.ExternalId)
                .HasDatabaseName("ix_op_history_externalid");
        }
    }
}
