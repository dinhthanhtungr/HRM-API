using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VietausWebAPI.Core.Domain.Entities.DevandqaSchema;

namespace VietausWebAPI.Infrastructure.DatabaseContext.Configurations.DevandqaSchema;

public sealed class CoaSpecificationItemConfiguration : IEntityTypeConfiguration<CoaSpecificationItem>
{
    public void Configure(EntityTypeBuilder<CoaSpecificationItem> entity)
    {
        entity.ToTable("CoaSpecificationItem", "devandqa");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.CoaSpecificationId).HasColumnName("coaspecificationid");
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
        entity.Property(x => x.ShowOnCoa).HasColumnName("showoncoa");
        entity.Property(x => x.SortOrder).HasColumnName("sortorder");
        entity.HasIndex(x => new { x.CoaSpecificationId, x.EvaluationStandardId }).IsUnique();
        entity.HasOne(x => x.CoaSpecification).WithMany(x => x.Items).HasForeignKey(x => x.CoaSpecificationId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.EvaluationStandard).WithMany().HasForeignKey(x => x.EvaluationStandardId).OnDelete(DeleteBehavior.Restrict);
    }
}
