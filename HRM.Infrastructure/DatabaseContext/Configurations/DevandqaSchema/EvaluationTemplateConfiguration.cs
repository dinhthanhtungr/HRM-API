using HRM.Domain.Entities.DevandqaSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.DevandqaSchema;

public sealed class EvaluationTemplateConfiguration : IEntityTypeConfiguration<EvaluationTemplate>
{
    public void Configure(EntityTypeBuilder<EvaluationTemplate> entity)
    {
        entity.ToTable("EvaluationTemplate", "devandqa", table =>
        {
            table.HasCheckConstraint("ck_evaluationtemplate_version", "version > 0");
            table.HasCheckConstraint("ck_evaluationtemplate_type", "type IN (1, 2, 3)");
            table.HasCheckConstraint("ck_evaluationtemplate_status", "status IN (0, 1, 2)");
            table.HasCheckConstraint("ck_evaluationtemplate_default", "NOT isdefault OR status = 1");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.CompanyId).HasColumnName("companyid");
        entity.Property(x => x.Code).HasColumnName("code").HasColumnType("citext").HasMaxLength(50).IsRequired();
        entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        entity.Property(x => x.Type).HasColumnName("type").HasConversion<int>();
        entity.Property(x => x.Version).HasColumnName("version");
        entity.Property(x => x.Status).HasColumnName("status").HasConversion<int>();
        entity.Property(x => x.IsDefault).HasColumnName("isdefault");
        entity.Property(x => x.EffectiveFrom).HasColumnName("effectivefrom").HasColumnType("timestamp without time zone");
        entity.Property(x => x.CreatedBy).HasColumnName("createdby");
        entity.Property(x => x.CreatedDate).HasColumnName("createddate").HasColumnType("timestamp without time zone");
        entity.Property(x => x.PublishedBy).HasColumnName("publishedby");
        entity.Property(x => x.PublishedDate).HasColumnName("publisheddate").HasColumnType("timestamp without time zone");
        entity.HasIndex(x => new { x.CompanyId, x.Type, x.Code, x.Version }).IsUnique();
        entity.HasIndex(x => new { x.CompanyId, x.Type }).IsUnique().HasFilter("isdefault = TRUE AND status = 1");
        entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.CreatedByEmployee).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.PublishedByEmployee).WithMany().HasForeignKey(x => x.PublishedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
