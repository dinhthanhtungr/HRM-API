using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.MROSchema;
public class TransferHeaderMROConfiguration : IEntityTypeConfiguration<TransferHeaderMRO>
{
    public void Configure(EntityTypeBuilder<TransferHeaderMRO> entity)
    {
        entity.ToTable("transferheaders", "mro");
        entity.HasKey(x => x.TransferHeaderId).HasName("pk_transferheaders");

        entity.Property(x => x.TransferHeaderId)
              .UseIdentityByDefaultColumn()
              .HasColumnName("transferheaders_id");

        entity.Property(x => x.TransferHeaderExternalId)
              .HasColumnName("transferheaders_externalid")
              .HasColumnType("text")
              .IsRequired();

        entity.Property(x => x.Note).HasColumnName("note").HasColumnType("text");
        entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        entity.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        entity.Property(x => x.Posted).HasColumnName("posted").HasDefaultValue(false).IsRequired();
        entity.Property(x => x.PostedAt).HasColumnName("posted_at");
        entity.Property(x => x.PostedBy).HasColumnName("posted_by");

        entity.HasIndex(x => x.TransferHeaderExternalId)
              .IsUnique()
              .HasDatabaseName("ux_transferheaders_externalid");

        entity.HasIndex(x => new { x.Posted, x.CreatedAt, x.TransferHeaderId })
              .IsDescending(false, true, true)
              .HasDatabaseName("ix_transferheaders_posted_created_desc");

        entity.HasIndex(x => x.CreatedBy).HasDatabaseName("ix_transferheaders_created_by");

        entity.HasOne(x => x.Creator)
              .WithMany()
              .HasForeignKey(x => x.CreatedBy)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_transferheaders_created_by");

        entity.HasOne(x => x.Postor)
              .WithMany()
              .HasForeignKey(x => x.PostedBy)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_transferheaders_posted_by");
    }
}
