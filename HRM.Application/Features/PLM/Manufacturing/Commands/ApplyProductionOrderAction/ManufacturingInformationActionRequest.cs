using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Commands.UpdateProductionOrderInformation;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Domain.Enums.Manufacturings;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.ApplyProductionOrderAction;

public sealed class ManufacturingInformationActionRequest
{
    [JsonRequired]
    public StepOfProduct? StepOfProduct { get; init; }

    [JsonRequired]
    public DateTime? ExpectedDate { get; init; }

    [JsonRequired]
    public decimal? TotalQuantity { get; init; }

    [JsonRequired]
    public int? NumOfBatches { get; init; }

    [MaxLength(20000)]
    public string? LabNote { get; init; }

    [MaxLength(20000)]
    public string? Requirement { get; init; }

    [MaxLength(20000)]
    public string? PlpuNote { get; init; }

    [MaxLength(20000)]
    public string? QcCheck { get; init; }
}
