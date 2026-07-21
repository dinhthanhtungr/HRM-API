using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.DeliverySchema
{
    public class DeliveryOrderDetailLotConsumption
    {
        public Guid Id { get; set; }

        public Guid DeliveryOrderDetailId { get; set; }
        public string LotNo { get; set; } = string.Empty;

        public decimal Quantity { get; set; }
        public decimal UnitCostSnapshot { get; set; }
        public decimal TotalCostSnapshot { get; set; }

        public Guid? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsActive { get; set; } = true;

        public virtual DeliveryOrderDetail DeliveryOrderDetail { get; set; } = null!;
        public virtual Employee? CreatedByNavigation { get; set; }
    }
}
