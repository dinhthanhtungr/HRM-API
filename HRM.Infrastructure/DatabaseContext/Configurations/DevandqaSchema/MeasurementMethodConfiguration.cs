using HRM.Domain.Entities.DevandqaSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.DevandqaSchema;

public sealed class MeasurementMethodConfiguration : IEntityTypeConfiguration<MeasurementMethod>
{
    public void Configure(EntityTypeBuilder<MeasurementMethod> entity)
    {
        entity.ToTable("MeasurementMethod", "devandqa", table => table.HasCheckConstraint("ck_measurementmethod_sortorder", "sortorder >= 0"));
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.CompanyId).HasColumnName("companyid");
        entity.Property(x => x.Code).HasColumnName("code").HasColumnType("citext").HasMaxLength(50).IsRequired();
        entity.Property(x => x.NameEn).HasColumnName("nameen").HasMaxLength(200);
        entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        entity.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        entity.Property(x => x.IsActive).HasColumnName("isactive").HasDefaultValue(true);
        entity.Property(x => x.SortOrder).HasColumnName("sortorder").HasDefaultValue(0);
        entity.Property(x => x.CreatedBy).HasColumnName("createdby");
        entity.Property(x => x.CreatedDate).HasColumnName("createddate").HasColumnType("timestamp without time zone");
        entity.Property(x => x.UpdatedBy).HasColumnName("updatedby");
        entity.Property(x => x.UpdatedDate).HasColumnName("updateddate").HasColumnType("timestamp without time zone");
        entity.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.CreatedByEmployee).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.UpdatedByEmployee).WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
