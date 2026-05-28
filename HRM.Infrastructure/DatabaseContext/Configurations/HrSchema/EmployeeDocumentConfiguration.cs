using HRM.Domain.Entities.HrSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.HrSchema
{
    public class EmployeeDocumentConfiguration : IEntityTypeConfiguration<EmployeeDocument>
    {
        public void Configure(EntityTypeBuilder<EmployeeDocument> entity)
        {
            entity.ToTable("employee_documents", "hr");

            entity.HasKey(e => e.EmployeeDocumentId).HasName("pk_employee_documents");

            entity.Property(e => e.EmployeeDocumentId).HasColumnName("employee_document_id").ValueGeneratedNever();
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.DocumentType).HasColumnName("document_type").IsRequired();
            entity.Property(e => e.DocumentName).HasColumnName("document_name").HasMaxLength(255);
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.Note).HasColumnName("note").HasColumnType("text");

            entity.HasIndex(e => e.EmployeeId).HasDatabaseName("ix_employee_documents_employee_id");

            entity.HasOne(e => e.Employee)
                  .WithMany(e => e.EmployeeDocuments)
                  .HasForeignKey(e => e.EmployeeId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_employee_documents_employee");
        }
    }
}
