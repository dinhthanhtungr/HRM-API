using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities;
using HRM.Domain.Entities.ManufacturingSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.ManufacturingSchema
{
    public class SchedualMfgConfiguration : IEntityTypeConfiguration<SchedualMfg>
    {
        public void Configure(EntityTypeBuilder<SchedualMfg> entity)
        {
            entity.HasKey(e => e.Idpk);

            entity.Property(e => e.Idpk)
                  .HasColumnName("Idpk")
                  .ValueGeneratedOnAdd();


            entity.ToTable("SchedualMfg", "Schedual");


            entity.Property(e => e.ProductId).HasColumnName("ProductId");
            entity.Property(e => e.MfgProductionOrderId).HasColumnName("MfgProductionOrderId");

            entity.Property(e => e.MachineId).HasColumnName("MachineId").HasColumnType("citext");
            entity.Property(e => e.Note).HasColumnName("Note").HasColumnType("citext");
            entity.Property(e => e.requirement).HasColumnName("requirement").HasColumnType("citext");
            entity.Property(e => e.Status).HasColumnName("Status").HasColumnType("citext");
            entity.Property(e => e.Qcstatus).HasColumnName("QCStatus").HasColumnType("citext");

            entity.Property(e => e.Number).HasColumnName("Number");
            entity.Property(e => e.Area).HasColumnName("Area");
            entity.Property(e => e.StepOfProduct).HasColumnName("StepOfProduct");



            entity.Property(e => e.ProductRecycleRate).HasColumnName("ProductRecycleRate");
            entity.Property(e => e.BTPStatus).HasColumnName("BTPStatus").HasColumnType("citext");

            entity.Property(e => e.ExpectedCompletionDate).HasColumnName("ExpectedCompletionDate");
            entity.Property(e => e.CreatedDate).HasColumnName("createdDate");
            entity.Property(e => e.PlanDate).HasColumnName("PlanDate");

            entity.HasOne(x => x.MfgProductionOrder)
                  .WithMany(a => a.SchedualMfgs)
                  .HasForeignKey(x => x.MfgProductionOrderId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_SchedualMfg_MfgProductionOrder_id");

            entity.HasOne(x => x.Product)
                  .WithMany(a => a.SchedualMfgs)
                  .HasForeignKey(x => x.ProductId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_SchedualMfg_Product_id");
        }
    }
}
