using VietausWebAPI.Core.Domain.Entities.DevandqaSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VietausWebAPI.Infrastructure.DatabaseContext.Configurations.DevandqaSchema;

/// <summary>Mỗi chỉ tiêu một dòng snapshot; tệp thuộc riêng kết quả và không cascade delete.</summary>
public sealed class LabTestResultConfiguration : IEntityTypeConfiguration<LabTestResult>
{
    public void Configure(EntityTypeBuilder<LabTestResult> entity)
    {
        entity.ToTable("LabTestResult", "devandqa", table => table.HasCheckConstraint(
            "ck_labtestresult_snapshot",
            "criterioncode <> '' AND criterionname <> '' AND datatype BETWEEN 1 AND 8 AND sortorder >= 0"));
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.LabTestId).HasColumnName("labtestid");
        entity.Property(x => x.EvaluationStandardId).HasColumnName("evaluationstandardid");
        entity.Property(x => x.SourceTemplateItemId).HasColumnName("sourcetemplateitemid");
        EvaluationSnapshotConfiguration.Configure(entity);
        entity.Property(x => x.Value).HasColumnName("value").HasColumnType("text");
        entity.Property(x => x.AttachmentCollectionId).HasColumnName("attachmentcollectionid");
        entity.HasIndex(x => new { x.LabTestId, x.CriterionCode }).IsUnique();
        entity.HasIndex(x => new { x.LabTestId, x.EvaluationStandardId }).IsUnique();
        entity.HasOne(x => x.LabTest).WithMany(x => x.Results).HasForeignKey(x => x.LabTestId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.EvaluationStandard).WithMany().HasForeignKey(x => x.EvaluationStandardId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.MeasurementMethod).WithMany().HasForeignKey(x => x.MeasurementMethodId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.AttachmentCollection).WithMany().HasForeignKey(x => x.AttachmentCollectionId).OnDelete(DeleteBehavior.Restrict);
    }
}
