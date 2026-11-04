using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VietausWebAPI.Core.Domain.Entities.DevandqaSchema;

namespace VietausWebAPI.Infrastructure.DatabaseContext.Configurations.DevandqaSchema;

public sealed class IqcInspectionEvaluationConfiguration : IEntityTypeConfiguration<IqcInspectionEvaluation>
{
    public void Configure(EntityTypeBuilder<IqcInspectionEvaluation> entity)
    {
        entity.ToTable("IqcInspectionEvaluation", "devandqa");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.QCInputByQCId).HasColumnName("qcinputbyqcid");
        entity.Property(x => x.IqcSpecificationId).HasColumnName("iqcspecificationid");
        entity.HasIndex(x => x.QCInputByQCId).IsUnique();
        entity.HasOne(x => x.QCInputByQC).WithMany().HasForeignKey(x => x.QCInputByQCId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.IqcSpecification).WithMany().HasForeignKey(x => x.IqcSpecificationId).OnDelete(DeleteBehavior.Restrict);
    }
}
