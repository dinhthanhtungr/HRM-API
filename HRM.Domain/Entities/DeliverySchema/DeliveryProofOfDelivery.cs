namespace HRM.Domain.Entities.DeliverySchema
{
    public class DeliveryProofOfDelivery
    {
        public Guid Id { get; set; }
        public Guid DeliveryOrderId { get; set; }

        public string? ReceiverName { get; set; }
        public string? ReceiverPhone { get; set; }
        public DateTime? ReceivedAt { get; set; }

        public string? SignatureUrl { get; set; }
        public string? PhotoUrl { get; set; }
        public string? Note { get; set; }

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        public Guid? ConfirmedBy { get; set; }
        public DateTime? ConfirmedDate { get; set; }

        public bool IsActive { get; set; } = true;

        public DeliveryOrder DeliveryOrder { get; set; } = default!;
    }
}
