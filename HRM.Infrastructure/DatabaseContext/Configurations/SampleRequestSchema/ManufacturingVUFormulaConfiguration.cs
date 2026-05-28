using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Infrastructure.DatabaseContext.Configurations.SampleRequestSchema
{
    public class ManufacturingVUFormulaConfiguration : IEntityTypeConfiguration<ManufacturingVUFormula>
    {
        public void Configure(EntityTypeBuilder<ManufacturingVUFormula> entity)
        {
            entity.ToTable("ManufacturingVUFormula", "SampleRequests");

            entity.HasKey(x => x.ManufacturingVUFormulaId);

            entity.Property(x => x.ManufacturingVUFormulaId)
                  .HasColumnName("manufacturingvuformulaid")
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(x => x.FormulaId)
                  .HasColumnName("formulaid");

            entity.Property(x => x.TotalProductionQuantity)
                  .HasColumnName("totalproductionquantity");

            entity.Property(x => x.NumOfBatches)
                  .HasColumnName("numofbatches");

            entity.Property(x => x.LabNote)
                  .HasColumnName("labnote")
                  .HasColumnType("citext"); // nếu bạn muốn case-insensitive như mẫu

            entity.Property(x => x.Requirement)
                  .HasColumnName("requirement")
                  .HasColumnType("citext");
            entity.Property(x => x.status)
                  .HasColumnName("status")
                  .HasConversion<int>()
                  .HasDefaultValueSql("1"); 
            entity.Property(x => x.QcCheck)
                  .HasColumnName("qccheck")
                  .HasColumnType("citext");

            entity.Property(x => x.CreatedDate)
                  .HasColumnName("createddate");

            entity.Property(x => x.CreatedBy)
                  .HasColumnName("createdby");

            entity.Property(x => x.UpdatedDate)
                  .HasColumnName("updateddate");

            entity.Property(x => x.UpdatedBy)
                  .HasColumnName("updatedby");

            // ====== RELATIONSHIPS ======

            entity.HasOne(d => d.Formula)
                  .WithMany(d => d.ManufacturingVUFormulas)
                  .HasForeignKey(d => d.FormulaId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_ManufacturingVUFormula_Formula");

            entity.HasOne(d => d.CreatedByNavigation)
                  .WithMany()
                  .HasForeignKey(d => d.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_ManufacturingVUFormula_CreatedBy");

            entity.HasOne(d => d.UpdatedByNavigation)
                  .WithMany()
                  .HasForeignKey(d => d.UpdatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_ManufacturingVUFormula_UpdatedBy");

            entity.HasMany(d => d.FormulaMaterialSnapshots)
                  .WithOne(d => d.ManufacturingVUFormula)
                  .HasForeignKey(d => d.ManufacturingVUFormulaId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_ManufacturingVUFormula_FormulaMaterialSnapshots");

            // ====== INDEXES ======
            entity.HasIndex(x => x.FormulaId)
                  .HasDatabaseName("ix_manufacturingvuformula_formulaid");

            entity.HasIndex(x => x.CreatedDate)
                  .HasDatabaseName("ix_manufacturingvuformula_createddate");
        }
    }
}
