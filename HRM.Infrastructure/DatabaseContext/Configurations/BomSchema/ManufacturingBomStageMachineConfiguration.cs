using HRM.Domain.Entities.BomSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.BomSchema;

public class ManufacturingBomStageMachineConfiguration : IEntityTypeConfiguration<ManufacturingBomStageMachine>
{
    public void Configure(EntityTypeBuilder<ManufacturingBomStageMachine> entity)
    {
        entity.ToTable("manufacturing_bom_stage_machines", "bom", table =>
            table.HasCheckConstraint("ck_manufacturing_bom_stage_machines_sequence_positive", "sequence_no > 0"));
        entity.HasKey(x => x.ManufacturingBomStageMachineId).HasName("pk_manufacturing_bom_stage_machines");
        entity.Property(x => x.ManufacturingBomStageMachineId).HasColumnName("manufacturing_bom_stage_machine_id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.ManufacturingBomStageId).HasColumnName("manufacturing_bom_stage_id").IsRequired();
        entity.Property(x => x.MachineCode).HasColumnName("machine_code").HasColumnType("citext").HasMaxLength(64).IsRequired();
        entity.Property(x => x.MachineName).HasColumnName("machine_name").HasMaxLength(200).IsRequired();
        entity.Property(x => x.IsDefault).HasColumnName("is_default").HasDefaultValue(false).IsRequired();
        entity.Property(x => x.SequenceNo).HasColumnName("sequence_no").IsRequired();
        entity.Property(x => x.Note).HasColumnName("note").HasColumnType("text");
        entity.HasIndex(x => new { x.ManufacturingBomStageId, x.MachineCode }).IsUnique().HasDatabaseName("ux_manufacturing_bom_stage_machines_stage_code");
        entity.HasOne(x => x.ManufacturingStage).WithMany(x => x.Machines).HasForeignKey(x => x.ManufacturingBomStageId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_manufacturing_bom_stage_machines_stage");
    }
}
