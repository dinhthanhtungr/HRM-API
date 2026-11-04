using HRM.Domain.Entities.DevandqaSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.DevandqaSchema;

public sealed class CoaCertificateResultConfiguration : IEntityTypeConfiguration<CoaCertificateResult>
{
    public void Configure(EntityTypeBuilder<CoaCertificateResult> entity)
    {
        entity.ToTable("CoaCertificateResult", "devandqa", table => table.HasCheckConstraint(
            "ck_coacertificateresult_snapshot",
            "NOT hasconfigurationsnapshot OR (evaluationstandardid IS NOT NULL AND criterioncode <> '' AND criterionname <> '' AND datatype BETWEEN 1 AND 8 AND sortorder >= 0)"));
        entity.HasKey(x => x.Id);
        entity.Property(x => x.AttachmentCollectionId).HasColumnName("attachmentcollectionid");
        entity.HasOne(x => x.AttachmentCollection).WithMany()
            .HasForeignKey(x => x.AttachmentCollectionId).OnDelete(DeleteBehavior.Restrict);
        EvaluationSnapshotConfiguration.Configure(entity);
        entity.Property(x => x.SourceTemplateItemId).HasColumnName("sourcetemplateitemid");
        entity.Property(x => x.EvaluationStandardId).HasColumnName("evaluationstandardid");
        entity.Property(x => x.HasConfigurationSnapshot).HasColumnName("hasconfigurationsnapshot");
        entity.HasOne(x => x.EvaluationStandard).WithMany().HasForeignKey(x => x.EvaluationStandardId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.MeasurementMethod).WithMany().HasForeignKey(x => x.MeasurementMethodId).OnDelete(DeleteBehavior.Restrict);

        entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.CoaCertificateId).HasColumnName("coacertificateid");
        entity.Property(x => x.CoaSpecificationItemId).HasColumnName("coaspecificationitemid");
        entity.Property(x => x.ExternalId).HasColumnName("externalid").HasColumnType("citext").HasMaxLength(50).IsRequired();
        entity.Property(x => x.Value).HasColumnName("value").HasColumnType("text");
        entity.HasIndex(x => new { x.CoaCertificateId, x.CoaSpecificationItemId }).IsUnique();
        entity.HasIndex(x => new { x.CoaCertificateId, x.ExternalId }).IsUnique();
        entity.HasOne(x => x.CoaCertificate).WithMany(x => x.Results).HasForeignKey(x => x.CoaCertificateId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.CoaSpecificationItem).WithMany().HasForeignKey(x => x.CoaSpecificationItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
