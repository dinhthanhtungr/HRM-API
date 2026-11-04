using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VietausWebAPI.Core.Domain.Entities.DevandqaSchema;

namespace VietausWebAPI.Infrastructure.DatabaseContext.Configurations.DevandqaSchema;

public sealed class CoaCertificateResultConfiguration : IEntityTypeConfiguration<CoaCertificateResult>
{
    public void Configure(EntityTypeBuilder<CoaCertificateResult> entity)
    {
        entity.ToTable("CoaCertificateResult", "devandqa");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.CoaCertificateId).HasColumnName("coacertificateid");
        entity.Property(x => x.CoaSpecificationItemId).HasColumnName("coaspecificationitemid");
        entity.Property(x => x.ExternalId).HasColumnName("externalid").HasColumnType("citext").HasMaxLength(50).IsRequired();
        entity.Property(x => x.Value).HasColumnName("value").HasColumnType("text");
        entity.HasIndex(x => new { x.CoaCertificateId, x.CoaSpecificationItemId }).IsUnique();
        entity.HasIndex(x => new { x.CoaCertificateId, x.ExternalId }).IsUnique();
        entity.HasOne(x => x.CoaCertificate).WithMany(x => x.Results).HasForeignKey(x => x.CoaCertificateId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.CoaSpecificationItem).WithMany().HasForeignKey(x => x.CoaSpecificationItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
