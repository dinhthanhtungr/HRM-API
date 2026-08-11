using HRM.Domain.Entities.DeliverySchema;

namespace HRM.Domain.Entities.OrderSchema;

public sealed class ComplaintReportLineLot
{
    public Guid ComplaintReportLineLotId { get; set; }
    public Guid ComplaintReportLineId { get; set; }
    public Guid SourceDeliveryOrderDetailId { get; set; }
    public Guid? SourceLotConsumptionId { get; set; }
    public string LotNoSnapshot { get; set; } = string.Empty;
    public decimal DeliveredQuantitySnapshot { get; set; }
    public decimal ComplaintQuantity { get; set; }
    public DateTime? DeliveredAtSnapshot { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedDate { get; set; }
    public Guid UpdatedBy { get; set; }

    public ComplaintReportLine ComplaintReportLine { get; set; } = null!;
    public DeliveryOrderDetail SourceDeliveryOrderDetail { get; set; } = null!;
    public DeliveryOrderDetailLotConsumption? SourceLotConsumption { get; set; }
}
