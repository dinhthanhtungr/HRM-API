using HRM.Domain.Entities.CustomerSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.CustomerSchema;

public sealed class CustomerSampleSharingGroupConfiguration : IEntityTypeConfiguration<CustomerSampleSharingGroup>
{
    public void Configure(EntityTypeBuilder<CustomerSampleSharingGroup> b)
    {
        b.ToTable("customer_sample_sharing_groups", "Customer");
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.CompanyId, x.Id });
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnType("timestamp without time zone");
        b.HasIndex(x => new { x.CompanyId, x.Name }).IsUnique().HasFilter("\"IsActive\" = TRUE");
        b.HasOne<HRM.Domain.Entities.CompanySchema.Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CustomerSampleSharingMemberConfiguration : IEntityTypeConfiguration<CustomerSampleSharingMember>
{
    public void Configure(EntityTypeBuilder<CustomerSampleSharingMember> b)
    {
        b.ToTable("customer_sample_sharing_members", "Customer");
        b.HasKey(x => x.Id);
        b.Property(x => x.CreatedAt).HasColumnType("timestamp without time zone");
        b.Property(x => x.RemovedAt).HasColumnType("timestamp without time zone");
        b.HasIndex(x => new { x.CompanyId, x.CustomerId }).IsUnique().HasFilter("\"IsActive\" = TRUE");
        b.HasIndex(x => new { x.CompanyId, x.GroupId, x.IsActive });
        b.HasOne(x => x.Group).WithMany().HasForeignKey(x => new { x.CompanyId, x.GroupId })
            .HasPrincipalKey(x => new { x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}
