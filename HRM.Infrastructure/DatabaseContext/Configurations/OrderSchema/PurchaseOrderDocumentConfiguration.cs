using HRM.Domain.Entities.OrderSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs.Configurations.OrderSchema;

public sealed class PurchaseOrderDocumentConfiguration : IEntityTypeConfiguration<PurchaseOrderDocument>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderDocument> entity)
    {
        entity.ToTable("PurchaseOrderDocuments", "Orders");
        entity.HasKey(x => x.PurchaseOrderDocumentId);
        entity.Property(x => x.PurchaseOrderDocumentId).ValueGeneratedNever();
        entity.Property(x => x.DocumentType).HasConversion<int>();
        entity.HasIndex(x => x.PurchaseOrderId);
        entity.HasOne(x => x.PurchaseOrder).WithMany().HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.AttachmentCollection).WithMany().HasForeignKey(x => x.AttachmentCollectionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.CreatedByNavigation).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.UpdatedByNavigation).WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.VerifiedByNavigation).WithMany().HasForeignKey(x => x.VerifiedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
