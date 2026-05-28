using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.DeliverySchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.DeliverySchema
{
    public sealed class DelivererInforConfiguration : IEntityTypeConfiguration<DelivererInfor>
    {
        public void Configure(EntityTypeBuilder<DelivererInfor> entity)
        {
            entity.ToTable("DelivererInfor", "DeliveryOrder");

            entity.HasKey(e => e.Id).HasName("PK__DelivererInfor__3214EC27B1E3D8F1");

            entity.Property(e => e.Id)
                  .HasDefaultValueSql("gen_random_uuid()")
                  .HasColumnName("ID");
        }
    }
}
