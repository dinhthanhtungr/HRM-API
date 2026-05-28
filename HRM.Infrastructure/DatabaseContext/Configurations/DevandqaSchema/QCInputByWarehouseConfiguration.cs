//using Microsoft.EntityFrameworkCore;
//using Microsoft.EntityFrameworkCore.Metadata.Builders;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using HRM.Domain.Entities.DevandqaSchema;

//namespace HRM.Infrastructure.DatabaseContext.Configurations.DevandqaSchema
//{
//    public class QCInputByWarehouseConfiguration : IEntityTypeConfiguration<QCInputByWarehouse>
//    {
//        public void Configure(EntityTypeBuilder<QCInputByWarehouse> entity)
//        {
//            entity.ToTable("QCInputByWarehouse", "devandqa");

//            entity.HasKey(x => x.QCInputByWarehouseId);

//            entity.Property(x => x.QCInputByWarehouseId)
//                  .HasColumnName("qcinputbywarehouseid")
//                  .HasDefaultValueSql("gen_random_uuid()");

//            entity.Property(x => x.MaterialId)
//                  .HasColumnName("materialid");

//            entity.Property(x => x.QCInputByQCId)
//                  .HasColumnName("qcinputbyqcid");

//            entity.Property(x => x.CSNameSnapshot)
//                  .HasColumnName("csnamesnapshot")
//                  .HasColumnType("citext");

//            entity.Property(x => x.CSExternalIdSnapshot)
//                  .HasColumnName("csexternalidsnapshot")
//                  .HasColumnType("citext");

//            entity.Property(x => x.MaterialExternalIdSnapshot)
//                  .HasColumnName("materialexternalidsnapshot")
//                  .HasColumnType("citext");

//            entity.Property(x => x.MaterialNameSnapshot)
//                  .HasColumnName("materialnamesnapshot")
//                  .HasColumnType("citext");

//            entity.Property(x => x.LotNo)
//                  .HasColumnName("lotno")
//                  .HasColumnType("citext");

//            entity.Property(x => x.CreatedDate)
//                  .HasColumnName("createddate");

//            entity.Property(x => x.CreatedBy)
//                  .HasColumnName("createdby");

//            // Relationships
//            entity.HasOne(d => d.Material)
//                  .WithMany(d => d.QCInputByWarehouses)
//                  .HasForeignKey(d => d.MaterialId)
//                  .OnDelete(DeleteBehavior.ClientSetNull)
//                  .HasConstraintName("FK_QCInputByWarehouse_Material");

//            entity.HasOne(d => d.QCInputByQC)
//                  .WithMany(d => d.QCInputByWarehouses)
//                  .HasForeignKey(d => d.QCInputByQCId)
//                  .OnDelete(DeleteBehavior.ClientSetNull)
//                  .HasConstraintName("FK_QCInputByWarehouse_QCInputByQC");

//            // Index gợi ý (lọc nhanh theo kho + truy vết)
//            entity.HasIndex(x => x.CreatedDate)
//                  .HasDatabaseName("ix_qcinputbywarehouse_createddate");

//            entity.HasIndex(x => x.MaterialId)
//                  .HasDatabaseName("ix_qcinputbywarehouse_materialid");

//            entity.HasIndex(x => x.QCInputByQCId)
//                  .HasDatabaseName("ix_qcinputbywarehouse_qcinputbyqcid");

//            entity.HasIndex(x => x.LotNo)
//                  .HasDatabaseName("ix_qcinputbywarehouse_lotno");

//            // Nếu bạn muốn tránh trùng lot cho cùng material+customer trong cùng 1 phiếu QC:
//            // entity.HasIndex(x => new { x.QCInputByQCId, x.MaterialId, x.CustomerId, x.LotNo })
//            //       .IsUnique()
//            //       .HasDatabaseName("ux_qcinputbywarehouse_qc_material_customer_lot");
//        }
//    }
//}
