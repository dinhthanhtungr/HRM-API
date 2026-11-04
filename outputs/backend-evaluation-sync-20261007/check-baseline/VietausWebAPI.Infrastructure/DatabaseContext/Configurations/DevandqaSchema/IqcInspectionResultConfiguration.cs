using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VietausWebAPI.Core.Domain.Entities.DevandqaSchema;

namespace VietausWebAPI.Infrastructure.DatabaseContext.Configurations.DevandqaSchema;

public sealed class IqcInspectionResultConfiguration : IEntityTypeConfiguration<IqcInspectionResult>
{
    public void Configure(EntityTypeBuilder<IqcInspectionResult> entity)
    {
        entity.ToTable("IqcInspectionResult", "devandqa");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.IqcInspectionEvaluationId).HasColumnName("iqcinspectionevaluationid");
        entity.Property(x => x.IqcSpecificationItemId).HasColumnName("iqcspecificationitemid");
        entity.Property(x => x.ExternalId).HasColumnName("externalid").HasColumnType("citext").HasMaxLength(50).IsRequired();
        entity.Property(x => x.Value).HasColumnName("value").HasColumnType("text");
        entity.HasIndex(x => new { x.IqcInspectionEvaluationId, x.IqcSpecificationItemId }).IsUnique();
        entity.HasIndex(x => new { x.IqcInspectionEvaluationId, x.ExternalId }).IsUnique();
        entity.HasOne(x => x.IqcInspectionEvaluation).WithMany(x => x.Results).HasForeignKey(x => x.IqcInspectionEvaluationId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.IqcSpecificationItem).WithMany().HasForeignKey(x => x.IqcSpecificationItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
