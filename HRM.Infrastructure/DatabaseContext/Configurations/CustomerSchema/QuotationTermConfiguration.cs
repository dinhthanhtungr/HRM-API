using HRM.Domain.Entities.CustomerSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.CustomerSchema;

public sealed class QuotationTermConfiguration : IEntityTypeConfiguration<QuotationTerm>
{
    public void Configure(EntityTypeBuilder<QuotationTerm> entity)
    {
        entity.ToTable("QuotationTerms", "Customer");
        entity.HasKey(x => x.QuotationTermId)
            .HasName("PK_QuotationTerms");

        entity.Property(x => x.QuotationTermId)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.QuotationId).IsRequired();
        entity.Property(x => x.LabelVi).HasMaxLength(200).IsRequired();
        entity.Property(x => x.LabelEn).HasMaxLength(200);
        entity.Property(x => x.ValueVi).HasMaxLength(1000);
        entity.Property(x => x.ValueEn).HasMaxLength(1000);
        entity.Property(x => x.SortOrder).HasDefaultValue(0);
        entity.Property(x => x.IsActive).HasDefaultValue(true);

        entity.HasIndex(x => new { x.QuotationId, x.SortOrder })
            .HasDatabaseName("IX_QuotationTerms_Quotation_SortOrder");

        entity.HasOne(x => x.Quotation)
            .WithMany(x => x.Terms)
            .HasForeignKey(x => x.QuotationId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_QuotationTerms_Quotation");
    }
}
