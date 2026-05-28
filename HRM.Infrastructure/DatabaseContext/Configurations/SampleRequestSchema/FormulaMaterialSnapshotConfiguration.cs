using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Infrastructure.Persistence.Configurations.SampleRequestSchema
{
    public class FormulaMaterialSnapshotConfiguration : IEntityTypeConfiguration<FormulaMaterialSnapshot>
    {
        public void Configure(EntityTypeBuilder<FormulaMaterialSnapshot> b)
        {
            b.ToTable("FormulaMaterialSnapshots", "SampleRequests"); // đổi schema/table nếu bạn đang dùng khác

            b.HasKey(x => x.FormulaMaterialSnapshotId);

            b.Property(x => x.FormulaMaterialSnapshotId)
             .ValueGeneratedNever();

            b.Property(x => x.ManufacturingVUFormulaId)
             .IsRequired();

            b.Property(x => x.CategoryId)
             .IsRequired();

            b.Property(x => x.Quantity)
             .HasColumnType("decimal(18,6)")
             .IsRequired();

            b.Property(x => x.UnitPrice)
             .HasPrecision(22, 6)
             .IsRequired();

            b.Property(x => x.TotalPrice)
             .HasPrecision(22, 6)
             .IsRequired();

            b.Property(x => x.itemType)
             .HasConversion<int>()   // enum -> int
             .IsRequired();

            b.Property(x => x.MaterialNameSnapshot)
             .HasMaxLength(500);

            b.Property(x => x.MaterialExternalIdSnapshot)
             .HasMaxLength(100);

            b.Property(x => x.LotNo);

            b.Property(x => x.Unit)
             .HasMaxLength(50);

            b.Property(x => x.IsActive)
             .HasDefaultValue(true)
             .IsRequired();

            // Relationship: Snapshot -> ManufacturingVUFormula (many-to-one)
            b.HasOne(x => x.ManufacturingVUFormula)
             .WithMany() // không cần navigation ngược trong entity
             .HasForeignKey(x => x.ManufacturingVUFormulaId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(x => x.ManufacturingVUFormulaId);
        }
    }
}