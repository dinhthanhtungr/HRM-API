using VietausWebAPI.Core.Domain.Entities.DevandqaSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VietausWebAPI.Infrastructure.DatabaseContext.Configurations.DevandqaSchema;

public sealed class EvaluationTemplateItemConfiguration : IEntityTypeConfiguration<EvaluationTemplateItem>
{
    public void Configure(EntityTypeBuilder<EvaluationTemplateItem> entity)
    {
        entity.ToTable("EvaluationTemplateItem", "devandqa");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.EvaluationTemplateId).HasColumnName("evaluationtemplateid");
        entity.Property(x => x.EvaluationStandardId).HasColumnName("evaluationstandardid");
        EvaluationSnapshotConfiguration.Configure(entity);
        entity.HasIndex(x => new { x.EvaluationTemplateId, x.EvaluationStandardId }).IsUnique();
        entity.HasIndex(x => new { x.EvaluationTemplateId, x.CriterionCode }).IsUnique();
        entity.HasOne(x => x.EvaluationTemplate).WithMany(x => x.Items).HasForeignKey(x => x.EvaluationTemplateId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.EvaluationStandard).WithMany().HasForeignKey(x => x.EvaluationStandardId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.MeasurementMethod).WithMany().HasForeignKey(x => x.MeasurementMethodId).OnDelete(DeleteBehavior.Restrict);
    }
}
