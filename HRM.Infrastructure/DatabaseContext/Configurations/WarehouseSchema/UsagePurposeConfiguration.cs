using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.WarehouseSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.WarehouseSchema
{
    public class UsagePurposeConfiguration : IEntityTypeConfiguration<UsagePurpose>
    {
        public void Configure(EntityTypeBuilder<UsagePurpose> entity)
        {
            entity.ToTable("UsagePurposes", "Warehouse");

            entity.HasKey(x => x.PurposeId)
                  .HasName("PK__UsagePurposes__purposeId");

            entity.Property(x => x.PurposeId)
                  .UseIdentityAlwaysColumn()
                  .HasColumnName("purposeId");

            entity.Property(x => x.PurposeCode)
                  .HasColumnName("purposeCode")
                  .HasColumnType("citext")
                  .IsRequired();

            entity.Property(x => x.PurposeName)
                  .HasColumnName("purposeName")
                  .IsRequired();

            entity.Property(x => x.PurposeNote)
                  .HasColumnName("purposeNote");

            entity.Property(x => x.IsActive)
                  .HasDefaultValue(true)
                  .HasColumnName("isActive");

            entity.Property(x => x.IsReceivable)
                  .HasDefaultValue(false)
                  .HasColumnName("isReceivable");

            entity.Property(x => x.IsPickable)
                  .HasDefaultValue(false)
                  .HasColumnName("isPickable");

            // Mã mục đích nên duy nhất
            entity.HasIndex(x => x.PurposeCode)
                  .IsUnique()
                  .HasDatabaseName("ux_usagepurposes_purposecode");
        }
    }
}
