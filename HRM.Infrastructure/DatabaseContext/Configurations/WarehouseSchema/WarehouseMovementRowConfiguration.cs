using HRM.Domain.Entities.WarehouseSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.WarehouseSchema;

public sealed class WarehouseMovementRowConfiguration : IEntityTypeConfiguration<WarehouseMovementRow>
{
    public void Configure(EntityTypeBuilder<WarehouseMovementRow> entity)
    {
        entity.HasNoKey();
        entity.ToView("v_WarehouseMovementCanonical", "Warehouse");
    }
}
