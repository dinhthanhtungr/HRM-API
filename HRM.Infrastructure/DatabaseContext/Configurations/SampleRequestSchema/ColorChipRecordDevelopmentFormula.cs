using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Infrastructure.Persistence.Configurations.SampleRequestSchema
{
    public class ColorChipRecordDevelopmentFormulaConfiguration
        : IEntityTypeConfiguration<ColorChipRecordDevelopmentFormula>
    {
        public void Configure(EntityTypeBuilder<ColorChipRecordDevelopmentFormula> b)
        {
            b.ToTable("color_chip_record_development_formulas", "SampleRequests");

            b.HasKey(x => x.ColorChipRecordDevelopmentFormulaId);

            b.Property(x => x.ColorChipRecordDevelopmentFormulaId)
                .HasColumnName("color_chip_record_development_formula_id")
                .HasDefaultValueSql("gen_random_uuid()");

            b.Property(x => x.ColorChipRecordId)
                .HasColumnName("color_chip_record_id")
                .IsRequired();

            b.Property(x => x.DevelopmentFormulaId)
                .HasColumnName("development_formula_id");

            b.Property(x => x.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true)
                .IsRequired();

            b.HasOne(x => x.ColorChipRecord)
                .WithMany(x => x.DevelopmentFormulas)
                .HasForeignKey(x => x.ColorChipRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.DevelopmentFormula)
                .WithMany()
                .HasForeignKey(x => x.DevelopmentFormulaId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasIndex(x => x.ColorChipRecordId)
                .HasDatabaseName("ix_ccrdf_color_chip_record_id");

            b.HasIndex(x => x.DevelopmentFormulaId)
                .HasDatabaseName("ix_ccrdf_development_formula_id");

        }
    }
}