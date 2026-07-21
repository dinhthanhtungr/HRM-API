using HRM.Domain.Entities.HrSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.HrSchema;

public sealed class SalaryComponentDefinitionConfiguration : IEntityTypeConfiguration<SalaryComponentDefinition>
{
    public void Configure(EntityTypeBuilder<SalaryComponentDefinition> entity)
    {
        entity.ToTable("SalaryComponentDefinitions", "hr");
        entity.HasKey(x => x.SalaryComponentDefinitionId);

        entity.Property(x => x.Code)
            .HasMaxLength(50)
            .IsRequired();

        entity.Property(x => x.Name)
            .HasMaxLength(255)
            .IsRequired();

        entity.Property(x => x.FormulaTemplate).HasColumnType("text");
        entity.Property(x => x.Note).HasColumnType("text");

        entity.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("UX_SalaryComponentDefinitions_Code");
    }
}
