using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Infrastructure.Persistence.Configurations.SampleRequestSchema
{
    public class ColorChipRecordConfiguration : IEntityTypeConfiguration<ColorChipRecord>
    {
        public void Configure(EntityTypeBuilder<ColorChipRecord> b)
        {
            b.ToTable("color_chip_records", "SampleRequests");

            b.HasKey(x => x.ColorChipRecordId);

            b.Property(x => x.ColorChipRecordId)
                .HasColumnName("color_chip_record_id")
                .HasDefaultValueSql("gen_random_uuid()");

            // =========================================================
            // 1. Classification / Business Type
            // =========================================================
            b.Property(x => x.RecordType)
                .HasColumnName("record_type")
                .HasConversion<int>()
                .IsRequired();

            b.Property(x => x.ResinType)
                .HasColumnName("resin_type")
                .HasConversion<int>()
                .IsRequired();

            b.Property(x => x.LogoType)
                .HasColumnName("logo_type")
                .HasConversion<int>()
                .IsRequired();

            b.Property(x => x.FormStyle)
                .HasColumnName("form_style")
                .HasConversion<int>()
                .IsRequired();

            // =========================================================
            // 2. Product
            // =========================================================
            b.Property(x => x.ProductId)
                .HasColumnName("product_id");

            // =========================================================
            // 3. Technical Information
            // =========================================================
            b.Property(x => x.Machine)
                .HasColumnName("machine")
                .HasMaxLength(200);

            b.Property(x => x.Resin)
                .HasColumnName("resin")
                .HasMaxLength(200);

            b.Property(x => x.TemperatureLimit)
                .HasColumnName("temperature_limit")
                .HasMaxLength(200);

            // =========================================================
            // 4. Physical / Form Information
            // =========================================================
            b.Property(x => x.SizeText)
                .HasColumnName("size_text")
                .HasMaxLength(100);

            b.Property(x => x.PelletWeightGram)
                .HasColumnName("pellet_weight_gram")
                .HasPrecision(18, 6);

            b.Property(x => x.NetWeightGram)
                .HasColumnName("net_weight_gram")
                .HasMaxLength(100);

            b.Property(x => x.Electrostatic)
                .HasColumnName("electrostatic");

            b.Property(x => x.Lightness)
                .HasColumnName("lightness")
                .HasColumnType("decimal(10,4)");

            b.Property(x => x.AValue)
                .HasColumnName("a_value")
                .HasColumnType("decimal(10,4)");

            b.Property(x => x.BValue)
                .HasColumnName("b_value")
                .HasColumnType("decimal(10,4)");

            // =========================================================
            // 5. Document / Record Info
            // =========================================================
            b.Property(x => x.AttachmentCollectionId)
                .HasColumnName("attachment_collection_id");

            b.Property(x => x.RecordDate)
                .HasColumnName("record_date");

            b.Property(x => x.Note)
                .HasColumnName("note")
                .HasMaxLength(2000);

            b.Property(x => x.PrintNote)
                .HasColumnName("print_note")
                .HasMaxLength(2000);

            // =========================================================
            // 6. Audit
            // =========================================================
            b.Property(x => x.CreatedDate)
                .HasColumnName("created_date")
                .IsRequired();

            b.Property(x => x.CreatedBy)
                .HasColumnName("created_by")
                .IsRequired();

            b.Property(x => x.UpdatedDate)
                .HasColumnName("updated_date");

            b.Property(x => x.UpdatedBy)
                .HasColumnName("updated_by");

            b.Property(x => x.CompanyId)
                .HasColumnName("company_id")
                .IsRequired();

            b.Property(x => x.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true)
                .IsRequired();

            // =========================================================
            // 7. Relationships
            // =========================================================
            b.HasOne(x => x.AttachmentCollection)
                .WithMany()
                .HasForeignKey(x => x.AttachmentCollectionId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Product)
                .WithMany(x => x.ColorChipRecords)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasMany(x => x.DevelopmentFormulas)
                .WithOne(x => x.ColorChipRecord)
                .HasForeignKey(x => x.ColorChipRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            // =========================================================
            // 8. Indexes
            // =========================================================
            b.HasIndex(x => new { x.ProductId, x.IsActive })
                .HasDatabaseName("ix_color_chip_records_product_id_is_active");

            b.HasIndex(x => new { x.RecordType, x.IsActive })
                .HasDatabaseName("ix_color_chip_records_record_type_is_active");

            b.HasIndex(x => new { x.RecordDate, x.IsActive })
                .HasDatabaseName("ix_color_chip_records_record_date_is_active");

            b.HasIndex(x => new { x.CompanyId, x.IsActive })
                .HasDatabaseName("ix_color_chip_records_company_id_is_active");
        }
    }
}