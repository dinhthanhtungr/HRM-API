using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.MROSchema;
public class TransferDetailMROConfiguration : IEntityTypeConfiguration<TransferDetailMRO>
{
    public void Configure(EntityTypeBuilder<TransferDetailMRO> entity)
    {
        entity.ToTable("transferdetails", "mro");
        entity.HasKey(x => x.TransferDetailId).HasName("pk_transferdetails");

        entity.Property(x => x.TransferDetailId)
              .UseIdentityByDefaultColumn()
              .HasColumnName("transferdetail_id");

        entity.Property(x => x.TransferHeaderId).HasColumnName("transferheaders_id").IsRequired();
        entity.Property(x => x.MaterialId).HasColumnName("material_id").IsRequired();
        entity.Property(x => x.FromSlotId).HasColumnName("fromslot_id");
        entity.Property(x => x.ToSlotId).HasColumnName("toslot_id");
        entity.Property(x => x.Qty).HasColumnName("qty").HasPrecision(18, 3).IsRequired();
        entity.Property(x => x.Note).HasColumnName("note").HasColumnType("text");

        entity.HasIndex(x => x.TransferHeaderId).HasDatabaseName("ix_transferdetails_header");
        entity.HasIndex(x => x.MaterialId).HasDatabaseName("ix_transferdetails_material");
        entity.HasIndex(x => x.FromSlotId).HasDatabaseName("ix_transferdetails_fromslot");
        entity.HasIndex(x => x.ToSlotId).HasDatabaseName("ix_transferdetails_toslot");

        entity.HasOne(x => x.Header)
              .WithMany(h => h.Details)
              .HasForeignKey(x => x.TransferHeaderId)
              .OnDelete(DeleteBehavior.Cascade)
              .HasConstraintName("fk_transferdetails_header");
    }
}
