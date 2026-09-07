using HRM.Domain.Entities.PrintectSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace HRM.Infrastructure.DatabaseContext.Configurations.PrintectSchema
{
    public class CustomerLabelDetailConfiguration : IEntityTypeConfiguration<CustomerLabelDetail>
    {
        public void Configure(EntityTypeBuilder<CustomerLabelDetail> entity)
        {
            entity.ToTable("CustomerLabelDetails", "printect");

            entity.HasKey(x => x.Id).HasName("PK_CustomerLabelDetails_Id");

            entity.Property(x => x.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(x => x.CustomerLabelHeaderId).HasColumnName("customerLabelHeaderId").IsRequired();
            entity.Property(x => x.LineNo).HasColumnName("lineNo").IsRequired();
            entity.Property(x => x.FieldKey)
                .HasColumnName("fieldKey")
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.FieldValue)
                .HasColumnName("fieldValue")
                .HasColumnType("text");

            entity.Property(x => x.IsActive)
                .HasColumnName("isActive")
                .HasDefaultValue(true);

            entity.HasIndex(x => x.CustomerLabelHeaderId)
                  .HasDatabaseName("IX_CustomerLabelDetails_HeaderId");

            entity.HasIndex(x => new { x.CustomerLabelHeaderId, x.FieldKey })
                  .IsUnique()
                  .HasDatabaseName("UX_CustomerLabelDetails_HeaderId_FieldKey");

            entity.HasOne(x => x.Header)
                .WithMany(h => h.Details)
                .HasForeignKey(x => x.CustomerLabelHeaderId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_CustomerLabelDetails_Header");
        }
    }
}
