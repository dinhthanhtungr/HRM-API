using VietausWebAPI.Core.Domain.Entities.DevandqaSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VietausWebAPI.Infrastructure.DatabaseContext.Configurations.DevandqaSchema;

public sealed class IqcSpecificationItemConfiguration : IEntityTypeConfiguration<IqcSpecificationItem>
{
    public void Configure(EntityTypeBuilder<IqcSpecificationItem> entity)
    {
        entity.ToTable("IqcSpecificationItem", "devandqa");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.MeasurementMethodId).HasColumnName("measurementmethodid");
        entity.HasOne(x => x.MeasurementMethod).WithMany().HasForeignKey(x => x.MeasurementMethodId).OnDelete(DeleteBehavior.Restrict);
        entity.Property(x => x.MeasurementMethodCode).HasColumnName("measurementmethodcode").HasMaxLength(50);

        entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.IqcSpecificationId).HasColumnName("iqcspecificationid");
        entity.Property(x => x.EvaluationStandardId).HasColumnName("evaluationstandardid");
        entity.Property(x => x.CriterionCode).HasColumnName("criterioncode").HasColumnType("citext").HasMaxLength(50).IsRequired();
        entity.Property(x => x.CriterionName).HasColumnName("criterionname").HasMaxLength(200).IsRequired();
        entity.Property(x => x.DataType).HasColumnName("datatype").HasConversion<int>();
        entity.Property(x => x.InputConfigJson).HasColumnName("inputconfig").HasColumnType("jsonb");
        entity.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(50);
        entity.Property(x => x.TestMethod).HasColumnName("testmethod").HasColumnType("text");
        entity.Property(x => x.AcceptanceRuleJson).HasColumnName("acceptancerule").HasColumnType("jsonb");
        entity.Property(x => x.SpecificationText).HasColumnName("specificationtext").HasColumnType("text");
        entity.Property(x => x.IsRequired).HasColumnName("isrequired");
        entity.Property(x => x.ShowOnReport).HasColumnName("showonreport");
        entity.Property(x => x.SortOrder).HasColumnName("sortorder");
        entity.HasIndex(x => new { x.IqcSpecificationId, x.EvaluationStandardId }).IsUnique();
        entity.HasOne(x => x.IqcSpecification).WithMany(x => x.Items).HasForeignKey(x => x.IqcSpecificationId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.EvaluationStandard).WithMany().HasForeignKey(x => x.EvaluationStandardId).OnDelete(DeleteBehavior.Restrict);
    }
}
