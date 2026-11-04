using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;

/// <summary>Dữ liệu người dùng nhập cho thao tác dùng hàng tồn; trạng thái do BE quyết định.</summary>
public sealed class UseProductionOrderStockRequest
{
    public decimal TotalQuantity { get; init; }
    public int NumOfBatches { get; init; }
    public StepOfProduct? StepOfProduct { get; init; }

    [JsonRequired]
    public DateTime? ExpectedDate { get; init; }

    [MaxLength(20000)]
    public string? PlpuNote { get; init; }

    [MaxLength(20000)]
    public string? LabNote { get; init; }

    [MaxLength(20000)]
    public string? Requirement { get; init; }

    [MaxLength(20000)]
    public string? QcCheck { get; init; }
}
