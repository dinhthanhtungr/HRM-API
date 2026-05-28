namespace HRM.Domain.Entities.DeliverySchema
{
    public class DeliveryTrip
    {
        public Guid Id { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        public DateTime TripDate { get; set; }
        public DateTime? PlannedStartTime { get; set; }
        public DateTime? PlannedEndTime { get; set; }
        public DateTime? ActualStartTime { get; set; }
        public DateTime? ActualEndTime { get; set; }

        public Guid? VehicleId { get; set; }
        public Guid? DriverId { get; set; }
        public Guid? DispatcherId { get; set; }

        public string? RouteCode { get; set; }
        public string? Note { get; set; }

        public bool IsActive { get; set; } = true;
        public Guid CompanyId { get; set; }

        public Guid CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public Guid? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }

        public DeliveryVehicle? Vehicle { get; set; }
        public DelivererInfor? Driver { get; set; }

        public ICollection<DeliveryTripOrder> Orders { get; set; } = new List<DeliveryTripOrder>();
        public ICollection<DeliveryStop> Stops { get; set; } = new List<DeliveryStop>();
        public ICollection<DeliveryExpense> Expenses { get; set; } = new List<DeliveryExpense>();
    }
}
