using VietausWebAPI.Core.Domain.Entities.DevandqaSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VietausWebAPI.Infrastructure.DatabaseContext.Configurations.DevandqaSchema;

/// <summary>Phiếu test theo company; mã duy nhất và quan hệ nguồn không cascade delete.</summary>
public sealed class LabTestConfiguration : IEntityTypeConfiguration<LabTest>
{
    public void Configure(EntityTypeBuilder<LabTest> entity)
    {
        entity.ToTable("LabTest", "devandqa");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.CompanyId).HasColumnName("companyid");
        entity.Property(x => x.Code).HasColumnName("code").HasColumnType("citext").HasMaxLength(50).IsRequired();
        entity.Property(x => x.SampleName).HasColumnName("samplename").HasMaxLength(200).IsRequired();
        entity.Property(x => x.TestDate).HasColumnName("testdate").HasColumnType("date");
        entity.Property(x => x.Note).HasColumnName("note").HasColumnType("text");
        entity.Property(x => x.EvaluationTemplateId).HasColumnName("evaluationtemplateid");
        entity.Property(x => x.TemplateCode).HasColumnName("templatecode").HasMaxLength(50);
        entity.Property(x => x.TemplateName).HasColumnName("templatename").HasMaxLength(200);
        entity.Property(x => x.TemplateVersion).HasColumnName("templateversion");
        entity.Property(x => x.CreatedBy).HasColumnName("createdby");
        entity.Property(x => x.CreatedDate).HasColumnName("createddate").HasColumnType("timestamp without time zone");
        entity.Property(x => x.UpdatedBy).HasColumnName("updatedby");
        entity.Property(x => x.UpdatedDate).HasColumnName("updateddate").HasColumnType("timestamp without time zone");
        entity.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        entity.HasIndex(x => new { x.CompanyId, x.TestDate });
        entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.EvaluationTemplate).WithMany().HasForeignKey(x => x.EvaluationTemplateId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.CreatedByEmployee).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.UpdatedByEmployee).WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
