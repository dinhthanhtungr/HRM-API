using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Domain.Enums.Formulas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulas;

public sealed record ManufacturingFormulaSummaryDto(
    Guid Id,
    FormulaSource Source,
    Guid ProductId,
    Guid? OrderId,
    string ExternalId,
    string Name,
    string? ColourCode,
    DateTime CreatedDate,
    decimal? TotalPrice,
    IReadOnlyList<ProductionOrderFormulaSourceItemDto> Items
);
