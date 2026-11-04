using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;

public sealed class UpdateProductionOrderInformationRequest
{
    // Legacy always assigns this field, including null. Requiring its presence avoids
    // silently clearing it when a new client omits the field in a partial update.
    [JsonRequired]
    public StepOfProduct? StepOfProduct { get; init; }

    [JsonRequired]
    public DateTime? ExpectedDate { get; init; }

    [JsonRequired]
    public decimal? TotalQuantity { get; init; }

    [JsonRequired]
    public int? NumOfBatches { get; init; }

    [MaxLength(100)]
    public string? Status { get; init; }

    [MaxLength(20000)]
    public string? LabNote { get; init; }

    [MaxLength(20000)]
    public string? Requirement { get; init; }

    [MaxLength(20000)]
    public string? PlpuNote { get; init; }

    [MaxLength(20000)]
    public string? QcCheck { get; init; }
}
