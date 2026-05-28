using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.SupplyRequestSchema;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs.Configurations.SupplyRequestSchema
{
    public class SupplyRequestDetailConfiguration : IEntityTypeConfiguration<SupplyRequestDetail>
    {
        public void Configure(EntityTypeBuilder<SupplyRequestDetail> entity)
        {
            entity.ToTable("SupplyRequestDetails", "SupplyRequest");

            entity.HasKey(x => x.DetailId)
                  .HasName("pk_supply_request_details");

            entity.Property(x => x.DetailId)
                  .UseIdentityByDefaultColumn()
                  .HasColumnName("detailid");

            entity.Property(x => x.SupplyRequestId)
                  .HasColumnName("requestid")
                  .IsRequired();

            entity.Property(x => x.MaterialId)
                  .HasColumnName("materialid")
                  .IsRequired();

            entity.Property(x => x.RequestedQuantity)
                  .HasColumnName("requestedquantity")
                  .IsRequired();

            entity.Property(x => x.RealQuantity)
                  .HasColumnName("real_quantity");

            entity.Property(x => x.Note)
                  .HasColumnName("note");

            entity.Property(x => x.ReceptDate)
                  .HasColumnName("recept_date");

            entity.HasIndex(x => x.SupplyRequestId)
                  .HasDatabaseName("ix_sr_details_requestid");

            entity.HasIndex(x => x.MaterialId)
                  .HasDatabaseName("ix_sr_details_materialid");

            // Unique: 1 material only once per request
            entity.HasIndex(x => new { x.SupplyRequestId, x.MaterialId })
                  .IsUnique()
                  .HasDatabaseName("ux_sr_details_request_material");

            entity.HasOne(x => x.SupplyRequest)
                  .WithMany(r => r.Details)
                  .HasForeignKey(x => x.SupplyRequestId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_sr_details_request");

            entity.HasOne(x => x.Material)
                  .WithMany(r => r.SupplyRequestDetails)
                  .HasForeignKey(x => x.MaterialId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_sr_details_material");
        }
    }
}
