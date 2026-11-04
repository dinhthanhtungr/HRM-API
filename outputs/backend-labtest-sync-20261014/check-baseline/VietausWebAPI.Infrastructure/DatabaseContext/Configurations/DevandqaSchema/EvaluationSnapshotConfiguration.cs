using VietausWebAPI.Core.Domain.Enums.Devandqa;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VietausWebAPI.Infrastructure.DatabaseContext.Configurations.DevandqaSchema;

/// <summary>Ánh xạ các cột snapshot trên dòng template và dòng phiếu.</summary>
internal static class EvaluationSnapshotConfiguration
{
    public static void Configure<TEntity>(EntityTypeBuilder<TEntity> entity) where TEntity : class
    {
        entity.Property<string>("CriterionCode").HasColumnName("criterioncode").HasColumnType("citext").HasMaxLength(50).IsRequired();
        entity.Property<string>("CriterionNameEn").HasColumnName("criterionnameen").HasMaxLength(200);
        entity.Property<string>("CriterionName").HasColumnName("criterionname").HasMaxLength(200).IsRequired();
        entity.Property<EvaluationDataType>("DataType").HasColumnName("datatype").HasConversion<int>();
        entity.Property<string>("Unit").HasColumnName("unit").HasMaxLength(50);
        entity.Property<Guid?>("MeasurementMethodId").HasColumnName("measurementmethodid");
        entity.Property<string>("MeasurementMethodCode").HasColumnName("measurementmethodcode").HasMaxLength(50);
        entity.Property<string>("TestMethod").HasColumnName("testmethod").HasColumnType("text");
        entity.Property<string>("InputConfigJson").HasColumnName("inputconfig").HasColumnType("jsonb");
        entity.Property<string>("AcceptanceRuleJson").HasColumnName("acceptancerule").HasColumnType("jsonb");
        entity.Property<string>("SpecificationText").HasColumnName("specificationtext").HasColumnType("text");
        entity.Property<bool>("IsRequired").HasColumnName("isrequired");
        entity.Property<int>("SortOrder").HasColumnName("sortorder");
    }
}
