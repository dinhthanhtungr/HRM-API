using HRM.Domain.Entities.PrintectSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.PrintectSchema;

public sealed class PrintLabelTemplateConfiguration : IEntityTypeConfiguration<PrintLabelTemplate>
{
    public void Configure(EntityTypeBuilder<PrintLabelTemplate> entity)
    {
        entity.ToTable("PrintLabelTemplates", "printect");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.CompanyId).HasColumnName("companyId").IsRequired();
        entity.Property(x => x.Code).HasColumnName("code").HasColumnType("citext").HasMaxLength(100).IsRequired();
        entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        entity.Property(x => x.LabelType).HasColumnName("labelType").HasColumnType("citext").HasMaxLength(100);
        entity.Property(x => x.Instructions).HasColumnName("instructions").HasColumnType("text");
        entity.Property(x => x.WidthMm).HasColumnName("widthMm").HasPrecision(10, 2);
        entity.Property(x => x.HeightMm).HasColumnName("heightMm").HasPrecision(10, 2);
        entity.Property(x => x.AttachmentCollectionId).HasColumnName("attachmentCollectionId");
        entity.Property(x => x.IsActive).HasColumnName("isActive").HasDefaultValue(true);
        entity.Property(x => x.CreatedBy).HasColumnName("createdBy");
        entity.Property(x => x.CreatedDate).HasColumnName("createdDate");
        entity.Property(x => x.UpdatedBy).HasColumnName("updatedBy");
        entity.Property(x => x.UpdatedDate).HasColumnName("updatedDate");
        entity.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasDatabaseName("UX_PrintLabelTemplates_Company_Code");
        entity.HasIndex(x => new { x.CompanyId, x.IsActive }).HasDatabaseName("IX_PrintLabelTemplates_Company_Active");
        entity.HasOne(x => x.AttachmentCollection).WithMany().HasForeignKey(x => x.AttachmentCollectionId)
            .OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_PrintLabelTemplates_AttachmentCollection");
    }
}
