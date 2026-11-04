using VietausWebAPI.Core.Domain.Entities.DevandqaSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VietausWebAPI.Infrastructure.DatabaseContext.Configurations.DevandqaSchema;

public sealed class CoaCertificateConfiguration : IEntityTypeConfiguration<CoaCertificate>
{
    public void Configure(EntityTypeBuilder<CoaCertificate> entity)
    {
        entity.ToTable("CoaCertificate", "devandqa");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.EvaluationTemplateId).HasColumnName("evaluationtemplateid");
        entity.Property(x => x.TemplateCode).HasColumnName("templatecode").HasMaxLength(50);
        entity.Property(x => x.TemplateName).HasColumnName("templatename").HasMaxLength(200);
        entity.Property(x => x.TemplateVersion).HasColumnName("templateversion");
        entity.HasOne(x => x.EvaluationTemplate).WithMany().HasForeignKey(x => x.EvaluationTemplateId).OnDelete(DeleteBehavior.Restrict);

        entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.CompanyId).HasColumnName("companyid");
        entity.Property(x => x.CoaSpecificationId).HasColumnName("coaspecificationid");
        entity.Property(x => x.CertificateNo).HasColumnName("certificateno").HasColumnType("citext").HasMaxLength(100).IsRequired();
        entity.Property(x => x.ProductCode).HasColumnName("productcode").HasColumnType("citext").HasMaxLength(100).IsRequired();
        entity.Property(x => x.ProductName).HasColumnName("productname").HasMaxLength(200).IsRequired();
        entity.Property(x => x.BatchNo).HasColumnName("batchno").HasColumnType("citext").HasMaxLength(100).IsRequired();
        entity.Property(x => x.ManufacturingDate).HasColumnName("manufacturingdate").HasColumnType("date");
        entity.Property(x => x.ExpiryDate).HasColumnName("expirydate").HasColumnType("date");
        entity.Property(x => x.Status).HasColumnName("status").HasConversion<int>();
        entity.Property(x => x.Notes).HasColumnName("notes").HasColumnType("text");
        entity.Property(x => x.IssuedBy).HasColumnName("issuedby");
        entity.Property(x => x.IssuedDate).HasColumnName("issueddate").HasColumnType("timestamp without time zone");
        entity.Property(x => x.CreatedBy).HasColumnName("createdby");
        entity.Property(x => x.CreatedDate).HasColumnName("createddate").HasColumnType("timestamp without time zone");
        entity.HasIndex(x => new { x.CompanyId, x.CertificateNo }).IsUnique();
        entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.CoaSpecification).WithMany().HasForeignKey(x => x.CoaSpecificationId).OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.CreatedByEmployee).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.IssuedByEmployee).WithMany().HasForeignKey(x => x.IssuedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
