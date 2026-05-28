namespace HRM.Domain.Entities.MROSchema
{
    // Ensure the correct type name is used. If the intended type is 'Warehouse', update the property accordingly.
    public class ZoneMRO
    {
        public int ZoneId { get; set; }
        public int WarehouseId { get; set; }
        public string ZoneName { get; set; } = null!;
        public string ZoneExternalId { get; set; } = null!;

        // Fix: Change 'Warehouses' to 'Warehouse' if that is the correct type.
        public WarehouseMRO Warehouse { get; set; } = null!;
        public ICollection<RackMRO> Racks { get; set; } = new HashSet<RackMRO>();
    }
}
