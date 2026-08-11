using HRM.Domain.Entities.CustomerSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.CustomerSchema;

public sealed class CustomerInteractionReferenceConfiguration : IEntityTypeConfiguration<CustomerInteractionReference>
{
    public void Configure(EntityTypeBuilder<CustomerInteractionReference> entity)
    {
        entity.HasKey(x => x.Id).HasName("PK_CustomerInteractionReferences_Id");
        entity.ToTable("CustomerInteractionReferences", "Customer");

        entity.Property(x => x.Id)
            .HasColumnName("Id")
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.InteractionId).HasColumnName("InteractionId").IsRequired();
        entity.Property(x => x.ReferenceType).HasColumnName("ReferenceType").HasConversion<int>().IsRequired();
        entity.Property(x => x.ReferenceId).HasColumnName("ReferenceId").IsRequired();
        entity.Property(x => x.ReferenceCodeSnapshot).HasColumnName("ReferenceCodeSnapshot").HasColumnType("citext");
        entity.Property(x => x.ReferenceNameSnapshot).HasColumnName("ReferenceNameSnapshot").HasColumnType("citext");
        entity.Property(x => x.IsPrimary).HasColumnName("IsPrimary").HasDefaultValue(false);
        entity.Property(x => x.CompanyId).HasColumnName("CompanyId").IsRequired();
        entity.Property(x => x.CreatedDate).HasColumnName("CreatedDate");
        entity.Property(x => x.CreatedBy).HasColumnName("CreatedBy");

        entity.HasIndex(x => new { x.CompanyId, x.ReferenceType, x.ReferenceId })
            .HasDatabaseName("IX_CustomerInteractionReferences_Company_Reference");

        entity.HasIndex(x => new { x.CompanyId, x.InteractionId })
            .HasDatabaseName("IX_CustomerInteractionReferences_Company_Interaction");

        entity.HasOne(x => x.Interaction)
            .WithMany(x => x.References)
            .HasForeignKey(x => x.InteractionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_CustomerInteractionReferences_Interaction");
    }
}
