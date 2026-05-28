using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.ForscadaSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.ForscadaSchema
{
    public class ProductionPlanPlpuConfiguration : IEntityTypeConfiguration<ProductionPlanPlpu>
    {
        public void Configure(EntityTypeBuilder<ProductionPlanPlpu> entity)
        {
            entity.ToTable("productionplan_plpu", "forscada");
            entity.HasNoKey();

            entity.Property(x => x.MachineId).HasColumnName("machineid").HasColumnType("text");
            entity.Property(x => x.Number).HasColumnName("number");
            entity.Property(x => x.Color).HasColumnName("color").HasColumnType("text");
            entity.Property(x => x.ProductionCode).HasColumnName("productioncode").HasColumnType("text");
            entity.Property(x => x.Note1).HasColumnName("note1").HasColumnType("text");
            entity.Property(x => x.Note2).HasColumnName("note2").HasColumnType("text");
            entity.Property(x => x.Note3).HasColumnName("note3").HasColumnType("text");
            entity.Property(x => x.BatchNo).HasColumnName("batchno").HasColumnType("text");
            entity.Property(x => x.RequestDate)
                  .HasColumnName("requestdate");
        }
    }
}
