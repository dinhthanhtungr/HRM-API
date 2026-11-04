using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.DevandqaSchema;

public sealed class CoaSpecificationConfiguration : IEntityTypeConfiguration<CoaSpecification>
{
    public void Configure(EntityTypeBuilder<CoaSpecification> entity)
    {
        entity.ToTable("CoaSpecification", "devandqa");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.CompanyId).HasColumnName("companyid");
        entity.Property(x => x.ProductId).HasColumnName("productid");
        entity.Property(x => x.Code).HasColumnName("code").HasColumnType("citext").HasMaxLength(50).IsRequired();
        entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        entity.Property(x => x.Version).HasColumnName("version");
        entity.Property(x => x.Status).HasColumnName("status").HasConversion<int>();
        entity.Property(x => x.EffectiveFrom).HasColumnName("effectivefrom").HasColumnType("timestamp without time zone");
        entity.Property(x => x.ApprovedBy).HasColumnName("approvedby");
        entity.Property(x => x.ApprovedDate).HasColumnName("approveddate").HasColumnType("timestamp without time zone");
        entity.Property(x => x.CreatedBy).HasColumnName("createdby");
        entity.Property(x => x.CreatedDate).HasColumnName("createddate").HasColumnType("timestamp without time zone");
        entity.HasIndex(x => new { x.CompanyId, x.Code, x.Version }).IsUnique();
        entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.CreatedByEmployee).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.ApprovedByEmployee).WithMany().HasForeignKey(x => x.ApprovedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
