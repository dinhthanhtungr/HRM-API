using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Enums.Attachment;

namespace HRM.Infrastructure.DatabaseContext.Configurations.DevandqaSchema
{
    public class QCInputByQCConfiguration : IEntityTypeConfiguration<QCInputByQC>
    {
        public void Configure(EntityTypeBuilder<QCInputByQC> entity)
        {
            entity.ToTable("QCInputByQC", "devandqa");

            // ===== PK =====
            entity.HasKey(x => x.QCInputByQCId);

            entity.Property(x => x.QCInputByQCId)
                  .HasColumnName("qcinputbyqcid")
                  .HasDefaultValueSql("gen_random_uuid()");

            // ===== FK =====
            entity.Property(x => x.AttachmentCollectionId)
                  .HasColumnName("attachmentcollectionid");

            //entity.Property(x => x.WarehouseRequestDetailId)
            //      .HasColumnName("warehouserequestdetailid");

            // ===== QC fields =====
            entity.Property(x => x.InspectionMethod)
                  .HasColumnName("inspectionmethod")
                  .HasColumnType("citext");

            entity.Property(x => x.IsCOAProvided)
                  .HasColumnName("iscoaprovided");

            entity.Property(x => x.IsMSDSTDSProvided)
                  .HasColumnName("ismsdstdsprovided");

            entity.Property(x => x.IsMetalDetectionRequired)
                  .HasColumnName("ismetaldetectionrequired");

            // ⚠ đang dùng QcDecision cho ImportWarehouseType (theo entity bạn đưa)
            entity.Property(x => x.ImportWarehouseType)
                  .HasColumnName("importwarehousetype");

            entity.Property(x => x.Note)
                  .HasColumnName("note")
                  .HasColumnType("citext");

            entity.Property(x => x.VoucherDetailId)
                  .HasColumnName("voucherdetailid");

            // ===== Audit =====
            entity.Property(x => x.CreatedDate)
                  .HasColumnName("createddate");

            entity.Property(x => x.CreatedBy)
                  .HasColumnName("createdby");

            // ===== Relationships =====
            entity.HasOne(x => x.Employee)
                  .WithMany()
                  .HasForeignKey(x => x.CreatedBy)
                  .HasConstraintName("FK_QCInputByQC_Employee");

            entity.HasOne(x => x.AttachmentCollection)
                  .WithMany()
                  .HasForeignKey(x => x.AttachmentCollectionId)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("FK_QCInputByQC_AttachmentCollection");

            entity.HasOne(x => x.WarehouseVoucherDetail)
                  .WithMany(x => x.QCInputByQCs)
                  .HasForeignKey(x => x.VoucherDetailId)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("FK_QCInputByQC_WarehouseVoucherDetail");
            // ===== Attachment upload tracking =====
            entity.Property(x => x.AttachmentStatus)
                  .HasColumnName("attachmentstatus")
                  .HasConversion<int>()
                  .HasDefaultValueSql("0");   // None = 0


            entity.Property(x => x.AttachmentLastError)
                  .HasColumnName("attachmentlasterror")
                  .HasColumnType("text");               // hoặc "citext" nếu bạn muốn

            entity.HasIndex(x => x.VoucherDetailId)
                  .HasDatabaseName("ix_qcinputbyqc_voucherdetailid");

            entity.HasIndex(x => x.CreatedDate)
                  .HasDatabaseName("ix_qcinputbyqc_createddate");

            entity.HasIndex(x => x.ImportWarehouseType)
                  .HasDatabaseName("ix_qcinputbyqc_importwarehousetype");
        }
    }
}
