using HRM.Domain.Entities.PrintectSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.PrintectSchema;

public sealed class PrintLabelLogoConfiguration : IEntityTypeConfiguration<PrintLabelLogo>
{
    public void Configure(EntityTypeBuilder<PrintLabelLogo> entity)
    {
        entity.ToTable("PrintLabelLogos", "printect");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.CompanyId).HasColumnName("companyId").IsRequired();
        entity.Property(x => x.ExternalId).HasColumnName("code").HasColumnType("citext").HasMaxLength(100).IsRequired();
        entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        entity.Property(x => x.AttachmentCollectionId).HasColumnName("attachmentCollectionId").IsRequired();
        entity.Property(x => x.IsActive).HasColumnName("isActive").HasDefaultValue(true);
        entity.Property(x => x.CreatedBy).HasColumnName("createdBy");
        entity.Property(x => x.CreatedDate).HasColumnName("createdDate");
        entity.Property(x => x.UpdatedBy).HasColumnName("updatedBy");
        entity.Property(x => x.UpdatedDate).HasColumnName("updatedDate");
        entity.HasIndex(x => new { x.CompanyId, x.ExternalId }).IsUnique().HasDatabaseName("UX_PrintLabelLogos_Company_Code");
        entity.HasOne(x => x.AttachmentCollection).WithMany().HasForeignKey(x => x.AttachmentCollectionId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_PrintLabelLogos_AttachmentCollection");
    }
}
