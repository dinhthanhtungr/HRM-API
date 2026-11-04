using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.PLM.ProductionOrders.Commands.CreateInternalProductionOrder;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Application.Features.PLM.ProductionOrders.Rules;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Manufacturings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.CreateBasicInternalProductionOrder;

public sealed class CreateBasicInternalProductionOrderRequest
{
    public Guid ProductId { get; init; }

    [JsonRequired]
    public Guid? FormulaId { get; init; }
    public Guid? CustomerId { get; init; }
    public DateTime? RequiredDate { get; init; }

    [JsonRequired]
    public DateTime? ExpectedDate { get; init; }

    [JsonRequired]
    public DateTime? ManufacturingDate { get; init; }
    public int TotalQuantityRequest { get; init; } = 1;
    public int? TotalQuantity { get; init; }
    public int? NumOfBatches { get; init; }
    public StepOfProduct? StepOfProduct { get; init; }

    [Required, MaxLength(255)]
    public string BagType { get; init; } = string.Empty;

    [MaxLength(2000)]
    public string? LabNote { get; init; }

    [MaxLength(2000)]
    public string? PlpuNote { get; init; }

    [MaxLength(2000)]
    public string? Requirement { get; init; }

    [MaxLength(2000)]
    public string? QcCheck { get; init; }
}
