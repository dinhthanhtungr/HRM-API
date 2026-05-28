namespace HRM.Domain.Entities.DeliverySchema
{
    public class DeliveryExpense
    {
        public Guid Id { get; set; }
        public Guid DeliveryTripId { get; set; }

        public string ExpenseType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public string? EvidenceUrl { get; set; }

        public Guid CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }

        public bool IsActive { get; set; } = true;

        public DeliveryTrip DeliveryTrip { get; set; } = default!;
    }
}
