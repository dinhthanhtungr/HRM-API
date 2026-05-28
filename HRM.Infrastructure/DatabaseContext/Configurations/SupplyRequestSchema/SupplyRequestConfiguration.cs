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
    public class SupplyRequestConfiguration : IEntityTypeConfiguration<SupplyRequest>
    {
        public void Configure(EntityTypeBuilder<SupplyRequest> entity)
        {
            entity.ToTable("SupplyRequests", "SupplyRequest");

            entity.HasKey(x => x.SupplyRequestId)
                  .HasName("pk_supply_requests");

            entity.Property(x => x.SupplyRequestId)
                  .HasDefaultValueSql("gen_random_uuid()")
                  .HasColumnName("requestid");

            entity.Property(x => x.ExternalId)
                  .HasMaxLength(16)
                  .HasColumnName("externalid")
                  .IsRequired();

            entity.HasIndex(x => x.ExternalId)
                  .IsUnique()
                  .HasDatabaseName("ux_supply_requests_externalid");

            entity.Property(x => x.RequestStatus)
                  .HasMaxLength(16)
                  .HasColumnName("requeststatus")
                  .IsRequired();

            entity.Property(x => x.Note)
                  .HasColumnName("note");

            entity.Property(x => x.CreatedDate)
                  .HasColumnName("created_date");

            entity.Property(x => x.CreatedBy)
                  .HasColumnName("createdby");

            entity.Property(x => x.IsActive)
                  .HasColumnName("isactive")
                  .HasDefaultValue(true);

            entity.HasIndex(x => x.CreatedBy)
                  .HasDatabaseName("ix_supply_requests_createdby");

            entity.HasIndex(x => new { x.RequestStatus, x.CreatedDate })
                  .HasDatabaseName("ix_supply_requests_status_createddate");

            entity.HasOne(x => x.CreatedByNavigation)
                  .WithMany(e => e.SupplyRequestCreatedByNavigations)
                  .HasForeignKey(x => x.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_supply_requests_createdby");
        }
    }
}
