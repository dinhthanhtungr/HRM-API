using HRM.Domain.Entities.DeliverySchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.DeliverySchema
{
    public sealed class DeliveryExpenseConfiguration : IEntityTypeConfiguration<DeliveryExpense>
    {
        public void Configure(EntityTypeBuilder<DeliveryExpense> entity)
        {
            entity.ToTable("DeliveryExpenses", "DeliveryOrder");

            entity.HasKey(e => e.Id).HasName("PK_DeliveryExpenses");

            entity.Property(e => e.Id)
                  .HasDefaultValueSql("gen_random_uuid()")
                  .HasColumnName("ID");

            entity.Property(e => e.ExpenseType).IsRequired().HasColumnType("citext");
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.EvidenceUrl).HasMaxLength(1000);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasIndex(e => e.DeliveryTripId, "IX_DeliveryExpenses_DeliveryTripId");
            entity.HasIndex(e => e.CreatedBy, "IX_DeliveryExpenses_CreatedBy");

            entity.HasOne(e => e.DeliveryTrip)
                  .WithMany(t => t.Expenses)
                  .HasForeignKey(e => e.DeliveryTripId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_DeliveryExpenses_Trip");
        }
    }
}
