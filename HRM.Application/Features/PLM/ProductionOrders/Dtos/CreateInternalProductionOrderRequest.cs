using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.ProductionOrders.Dtos;

/// <summary>Tạo MFG nội bộ; không tạo VA, lịch sản xuất hoặc liên kết đơn hàng.</summary>
public sealed class CreateInternalProductionOrderRequest
{
    public Guid ProductId { get; set; }
    public DateTime RequiredDate { get; set; }
    public int TotalQuantityRequest { get; set; }
    public decimal UnitPriceAgreed { get; set; }
    public string BagType { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public Guid? FormulaId { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public DateTime? ManufacturingDate { get; set; }
    public int? NumOfBatches { get; set; }
    public int? TotalQuantity { get; set; }
    public string? LabNote { get; set; }
    public string? Requirement { get; set; }
    public string? PlpuNote { get; set; }
    public string? QcCheck { get; set; }
    public StepOfProduct? StepOfProduct { get; set; }
    public string? InitialStatus { get; set; }
    // Compatibility: field này trong DTO cũ không được CreateInternalAsync sử dụng.
    public Guid? MerchandiseOrderDetailId { get; set; }
}
