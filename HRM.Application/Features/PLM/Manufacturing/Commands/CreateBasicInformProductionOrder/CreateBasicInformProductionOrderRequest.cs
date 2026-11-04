using System.ComponentModel.DataAnnotations;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.CreateBasicInformProductionOrder;

public sealed class CreateBasicInformProductionOrderRequest
{
    public Guid MerchandiseOrderId { get; init; }
    public Guid MerchandiseOrderDetailId { get; init; }
    public Guid? FormulaId { get; init; }
    public Guid? ManufacturingFormulaId { get; init; }
    public DateTime RequiredDate { get; init; }
    public DateTime? ExpectedDate { get; init; }
    public DateTime? ManufacturingDate { get; init; }
    public decimal TotalQuantityRequest { get; init; }
    public decimal? TotalQuantity { get; init; }
    public int? NumOfBatches { get; init; }
    public StepOfProduct? StepOfProduct { get; init; }

    [MaxLength(255)]
    public string? BagType { get; init; }

    [MaxLength(2000)]
    public string? LabNote { get; init; }

    [MaxLength(2000)]
    public string? Requirement { get; init; }

    [MaxLength(2000)]
    public string? PlpuNote { get; init; }

    [MaxLength(2000)]
    public string? QcCheck { get; init; }
    public List<ManufacturingFormulaInputItem>? Items { get; init; }
}
