using VietausWebAPI.Core.Domain.Entities.DevandqaSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VietausWebAPI.Infrastructure.DatabaseContext.Configurations.DevandqaSchema;

public sealed class IqcInspectionEvaluationConfiguration : IEntityTypeConfiguration<IqcInspectionEvaluation>
{
    public void Configure(EntityTypeBuilder<IqcInspectionEvaluation> entity)
    {
        entity.ToTable("IqcInspectionEvaluation", "devandqa");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.EvaluationTemplateId).HasColumnName("evaluationtemplateid");
        entity.Property(x => x.TemplateCode).HasColumnName("templatecode").HasMaxLength(50);
        entity.Property(x => x.TemplateName).HasColumnName("templatename").HasMaxLength(200);
        entity.Property(x => x.TemplateVersion).HasColumnName("templateversion");
        entity.HasOne(x => x.EvaluationTemplate).WithMany().HasForeignKey(x => x.EvaluationTemplateId).OnDelete(DeleteBehavior.Restrict);

        entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.QCInputByQCId).HasColumnName("qcinputbyqcid");
        entity.Property(x => x.IqcSpecificationId).HasColumnName("iqcspecificationid");
        entity.HasIndex(x => x.QCInputByQCId).IsUnique();
        entity.HasOne(x => x.QCInputByQC).WithMany().HasForeignKey(x => x.QCInputByQCId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.IqcSpecification).WithMany().HasForeignKey(x => x.IqcSpecificationId).OnDelete(DeleteBehavior.Restrict);
    }
}
