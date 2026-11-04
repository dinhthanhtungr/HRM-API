using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrder;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Domain.Enums.Formulas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulaEditor;

public sealed record ManufacturingFormulaEditorDto(
    GetMfgProductionOrder Order,
    Guid? FormulaId,
    string? ExternalId,
    string? Name,
    string? Status,
    Guid SourceFormulaId,
    FormulaSource SourceType,
    string? SourceCode,
    DateTime SourceCreatedDate,
    string? Note,
    bool IsSelected,
    bool IsStandard,
    string? CurrentProductName,
    string? CurrentColourCode,
    bool IsRecycle,
    bool PricesVisible,
    decimal? TotalPrice,
    IReadOnlyList<ProductionOrderFormulaSourceItemDto> Items
);
