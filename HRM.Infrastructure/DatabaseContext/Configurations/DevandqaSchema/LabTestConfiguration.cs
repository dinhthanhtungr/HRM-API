using HRM.Domain.Entities.DevandqaSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.DevandqaSchema;

/// <summary>Phiếu test theo company; mã duy nhất và quan hệ nguồn không cascade delete.</summary>
public sealed class LabTestConfiguration : IEntityTypeConfiguration<LabTest>
{
    public void Configure(EntityTypeBuilder<LabTest> entity)
    {
        entity.ToTable("LabTest", "devandqa");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.CompanyId).HasColumnName("companyid");
        entity.Property(x => x.SupplierId).HasColumnName("supplierid").IsRequired(false);
        entity.Property(x => x.SupplierName).HasColumnName("suppliername").HasMaxLength(300).IsRequired(false);
        entity.Property(x => x.MaterialId).HasColumnName("materialid").IsRequired(false);
        entity.Property(x => x.MaterialCode).HasColumnName("materialcode").HasColumnType("citext");
        entity.Property(x => x.Code).HasColumnName("code").HasColumnType("citext").HasMaxLength(50).IsRequired();
        entity.Property(x => x.SampleName).HasColumnName("samplename").HasMaxLength(200).IsRequired();
        entity.Property(x => x.BatchNumber).HasColumnName("batchnumber").HasMaxLength(100);
        entity.Property(x => x.WeightKg).HasColumnName("weightkg").HasPrecision(18, 6);
        /* Tạm dừng mapping mở rộng và index import cùng entity.
        entity.Property(x => x.SupplierCustomerName).HasColumnName("suppliercustomername").HasMaxLength(300);
        entity.Property(x => x.ItemCode).HasColumnName("itemcode").HasMaxLength(100);
        entity.Property(x => x.Resin).HasColumnName("resin").HasMaxLength(100);
        entity.Property(x => x.Application).HasColumnName("application").HasMaxLength(2000);
        entity.Property(x => x.PreparedByName).HasColumnName("preparedbyname").HasMaxLength(200);
        entity.Property(x => x.ImportedPhotoName).HasColumnName("importedphotoname").HasMaxLength(255);
        entity.Property(x => x.IsDeleted).HasColumnName("isdeleted").HasDefaultValue(false);
        entity.Property(x => x.ImportBatchId).HasColumnName("importbatchid");
        entity.Property(x => x.ImportRowNumber).HasColumnName("importrownumber");
        entity.HasIndex(x => new { x.CompanyId, x.ImportBatchId, x.ImportRowNumber }).IsUnique();
        */
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
        entity.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.EvaluationTemplate).WithMany().HasForeignKey(x => x.EvaluationTemplateId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.CreatedByEmployee).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.UpdatedByEmployee).WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
