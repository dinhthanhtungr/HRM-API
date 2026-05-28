namespace HRM.Domain.Entities.DeliverySchema
{
    public class DeliveryVehicle
    {
        public Guid Id { get; set; }
        public string PlateNumber { get; set; } = string.Empty;
        public string? VehicleType { get; set; }
        public decimal? MaxLoadKg { get; set; }
        public decimal? MaxVolumeM3 { get; set; }

        public bool IsInternal { get; set; } = true;
        public string? OwnerName { get; set; }
        public string? Phone { get; set; }
        public string? Note { get; set; }

        public bool IsActive { get; set; } = true;
        public Guid CompanyId { get; set; }

        public ICollection<DeliveryTrip> Trips { get; set; } = new List<DeliveryTrip>();
    }
}
