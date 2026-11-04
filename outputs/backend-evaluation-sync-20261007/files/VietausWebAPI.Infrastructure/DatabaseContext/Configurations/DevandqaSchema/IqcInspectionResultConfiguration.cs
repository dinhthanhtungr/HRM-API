using VietausWebAPI.Core.Domain.Entities.DevandqaSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VietausWebAPI.Infrastructure.DatabaseContext.Configurations.DevandqaSchema;

public sealed class IqcInspectionResultConfiguration : IEntityTypeConfiguration<IqcInspectionResult>
{
    public void Configure(EntityTypeBuilder<IqcInspectionResult> entity)
    {
        entity.ToTable("IqcInspectionResult", "devandqa", table => table.HasCheckConstraint(
            "ck_iqcinspectionresult_snapshot",
            "NOT hasconfigurationsnapshot OR (evaluationstandardid IS NOT NULL AND criterioncode <> '' AND criterionname <> '' AND datatype BETWEEN 1 AND 7 AND sortorder >= 0)"));
        entity.HasKey(x => x.Id);
        EvaluationSnapshotConfiguration.Configure(entity);
        entity.Property(x => x.SourceTemplateItemId).HasColumnName("sourcetemplateitemid");
        entity.Property(x => x.EvaluationStandardId).HasColumnName("evaluationstandardid");
        entity.Property(x => x.HasConfigurationSnapshot).HasColumnName("hasconfigurationsnapshot");
        entity.HasOne(x => x.SourceTemplateItem).WithMany().HasForeignKey(x => x.SourceTemplateItemId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.EvaluationStandard).WithMany().HasForeignKey(x => x.EvaluationStandardId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.MeasurementMethod).WithMany().HasForeignKey(x => x.MeasurementMethodId).OnDelete(DeleteBehavior.Restrict);

        entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.IqcInspectionEvaluationId).HasColumnName("iqcinspectionevaluationid");
        entity.Property(x => x.IqcSpecificationItemId).HasColumnName("iqcspecificationitemid");
        entity.Property(x => x.ExternalId).HasColumnName("externalid").HasColumnType("citext").HasMaxLength(50).IsRequired();
        entity.Property(x => x.Value).HasColumnName("value").HasColumnType("text");
        entity.HasIndex(x => new { x.IqcInspectionEvaluationId, x.IqcSpecificationItemId }).IsUnique();
        entity.HasIndex(x => new { x.IqcInspectionEvaluationId, x.ExternalId }).IsUnique();
        entity.HasOne(x => x.IqcInspectionEvaluation).WithMany(x => x.Results).HasForeignKey(x => x.IqcInspectionEvaluationId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.IqcSpecificationItem).WithMany().HasForeignKey(x => x.IqcSpecificationItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
