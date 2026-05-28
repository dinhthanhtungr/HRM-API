using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.ManufacturingSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.ManufacturingSchema
{
    /// <summary>manufacturing.MfgProductionOrders</summary>
    public class MfgProductionOrderConfiguration : IEntityTypeConfiguration<MfgProductionOrder>
    {
        public void Configure(EntityTypeBuilder<MfgProductionOrder> entity)
        {
            entity.ToTable("MfgProductionOrders", "manufacturing");

            entity.HasKey(e => e.MfgProductionOrderId)
                  .HasName("PK__MfgProductionOrders__MfgProductionOrderId");

            entity.Property(e => e.MfgProductionOrderId)
                  .HasDefaultValueSql("gen_random_uuid()")
                  .HasColumnName("mfgProductionOrderId");

            entity.Property(e => e.ExternalId)
                  .HasColumnName("external_id")
                  .HasColumnType("citext")
                  .IsRequired();

            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.ProductExternalIdSnapshot).HasColumnName("product_externalid_snapshot").HasColumnType("citext");
            entity.Property(e => e.ProductNameSnapshot).HasColumnName("product_name_snapshot").HasColumnType("citext");
            entity.Property(e => e.ColorName).HasColumnName("color_name").HasColumnType("citext");

            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.CustomerNameSnapshot).HasColumnName("customer_name_snapshot").HasColumnType("citext");
            entity.Property(e => e.CustomerExternalIdSnapshot).HasColumnName("customer_externalid_snapshot").HasColumnType("citext");

            entity.Property(e => e.FormulaId).HasColumnName("formula_id");
            entity.Property(e => e.FormulaExternalIdSnapshot).HasColumnName("formula_externalid_snapshot").HasColumnType("citext");

            entity.Property(e => e.ManufacturingDate).HasColumnName("manufacturing_date");
            entity.Property(e => e.ExpectedDate).HasColumnName("expected_date");
            entity.Property(e => e.RequiredDate).HasColumnName("required_date");

            entity.Property(e => e.TotalQuantityRequest).HasColumnName("total_quantity_request").HasPrecision(18, 3);
            entity.Property(e => e.TotalQuantity).HasColumnName("total_quantity").HasPrecision(18, 3);
            entity.Property(e => e.NumOfBatches).HasColumnName("num_of_batches");

            entity.Property(e => e.UnitPriceAgreed)
                  .HasColumnName("unit_price_agreed")
                  .HasPrecision(18, 2);

            entity.Property(e => e.Status)
                  .HasColumnName("status")
                  .HasColumnType("citext");

            entity.Property(e => e.LabNote).HasColumnName("lab_note");
            entity.Property(e => e.Requirement).HasColumnName("requirement");
            entity.Property(e => e.PlpuNote).HasColumnName("plpu_note");
            entity.Property(e => e.BagType).HasColumnName("bag_type");

            entity.Property(e => e.QcCheck).HasColumnName("qc_check");
            entity.Property(e => e.StepOfProduct).HasColumnName("step_of_product");

            entity.Property(e => e.IsActive)
                  .HasColumnName("is_active")
                  .HasDefaultValue(true)
                  .IsRequired();

            entity.Property(e => e.CompanyId).HasColumnName("company_id");

            entity.Property(e => e.CreatedDate).HasColumnName("created_date");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedDate).HasColumnName("updated_date");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasIndex(e => new { e.CompanyId, e.ExternalId })
                  .IsUnique()
                  .HasDatabaseName("ux_mpo_company_externalid");

            entity.HasIndex(e => new { e.CompanyId, e.IsActive, e.CreatedDate, e.MfgProductionOrderId })
                  .IsDescending(false, false, true, true)
                  .HasDatabaseName("ix_mpo_company_active_createddesc");

            entity.HasIndex(e => new { e.CompanyId, e.Status, e.ExpectedDate, e.MfgProductionOrderId })
                  .IsDescending(false, false, true, true)
                  .HasDatabaseName("ix_mpo_company_status_expecteddesc");

            entity.HasIndex(e => new { e.CompanyId, e.Status, e.RequiredDate, e.MfgProductionOrderId })
                  .IsDescending(false, false, true, true)
                  .HasDatabaseName("ix_mpo_company_status_requireddesc");

            entity.HasIndex(e => new { e.CompanyId, e.ProductId, e.IsActive })
                  .HasDatabaseName("ix_mpo_company_product_active");

            entity.HasOne(d => d.Product).WithMany(p => p.MfgProductionOrders)
                  .HasForeignKey(d => d.ProductId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mpo__productId");

            entity.HasOne(d => d.Formula).WithMany(p => p.ManufacturingProductionOrders)
                  .HasForeignKey(d => d.FormulaId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mpo__formulaId");


            entity.HasOne(d => d.Customer).WithMany(p => p.MfgProductionOrders)
                  .HasForeignKey(d => d.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mpo__customerId");

            entity.HasOne(d => d.Company).WithMany(p => p.MfgProductionOrders)
                  .HasForeignKey(d => d.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mpo__companyId");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.MfgProductionOrderCreatedByNavigations)
                  .HasForeignKey(d => d.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mpo__createdBy");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.MfgProductionOrderUpdatedByNavigations)
                  .HasForeignKey(d => d.UpdatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mpo__updatedBy");
        }
    }
}
