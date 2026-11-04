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

public sealed class ManufacturingFormulaListOptions
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 15;
    public Guid? ProductId { get; init; }
    public FormulaSource Source { get; init; } = FormulaSource.Both;
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public string? Keyword { get; init; }
}
