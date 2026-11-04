using VietausWebAPI.Core.Domain.Entities.DevandqaSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VietausWebAPI.Core.Domain.Enums.Devandqa;

namespace VietausWebAPI.Infrastructure.DatabaseContext.Configurations.DevandqaSchema;

/// <summary>Ánh xạ tiêu chuẩn đánh giá theo công ty; người tạo/cập nhật dùng CreatedBy/UpdatedBy, không có FK người duyệt riêng.</summary>
public sealed class EvaluationStandardConfiguration : IEntityTypeConfiguration<EvaluationStandard>
{
    public void Configure(EntityTypeBuilder<EvaluationStandard> entity)
    {
        entity.ToTable("EvaluationStandard", "devandqa");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.MeasurementMethodId).HasColumnName("measurementmethodid");
        entity.HasOne(x => x.MeasurementMethod).WithMany().HasForeignKey(x => x.MeasurementMethodId).OnDelete(DeleteBehavior.Restrict);

        entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.Code).HasColumnName("code").HasColumnType("citext").HasMaxLength(50).IsRequired();
        entity.Property(x => x.NameEn).HasColumnName("nameen").HasMaxLength(200);
        entity.Property(x => x.Name).HasColumnName("name").HasColumnType("citext").HasMaxLength(200).IsRequired();
        entity.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        entity.Property(x => x.DataType).HasColumnName("datatype").HasConversion<int>().HasDefaultValue(EvaluationDataType.Text);
        entity.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(50);
        entity.Property(x => x.TestMethod).HasColumnName("testmethod").HasColumnType("text");
        entity.Property(x => x.InputConfigJson).HasColumnName("inputconfig").HasColumnType("jsonb");
        entity.Property(x => x.AcceptanceRuleJson).HasColumnName("acceptancerule").HasColumnType("jsonb");
        entity.Property(x => x.SpecificationText).HasColumnName("specificationtext").HasColumnType("text");
        entity.Property(x => x.IsRequired).HasColumnName("isrequired").HasDefaultValue(false);
        entity.Property(x => x.CompanyId).HasColumnName("companyid");
        entity.Property(x => x.IsActive).HasColumnName("isactive").HasDefaultValue(true);
        entity.Property(x => x.SortOrder).HasColumnName("sortorder").HasDefaultValue(0);
        entity.Property(x => x.CreatedDate).HasColumnName("createddate");
        entity.Property(x => x.CreatedBy).HasColumnName("createdby");
        entity.Property(x => x.UpdatedDate).HasColumnName("updateddate");
        entity.Property(x => x.UpdatedBy).HasColumnName("updatedby");

        entity.HasIndex(x => new { x.CompanyId, x.Code })
            .IsUnique().HasDatabaseName("ux_evaluationstandard_company_code");
        entity.HasOne(x => x.Company).WithMany(x => x.EvaluationStandards).HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_evaluationstandard_company");
        entity.HasOne(x => x.CreatedByEmployee).WithMany(x => x.EvaluationStandardCreatedByNavigations)
            .HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.UpdatedByEmployee).WithMany(x => x.EvaluationStandardUpdatedByNavigations)
            .HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);

    }
}
