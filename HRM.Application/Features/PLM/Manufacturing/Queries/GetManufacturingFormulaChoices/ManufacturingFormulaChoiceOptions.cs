using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulaChoices;

public sealed class ManufacturingFormulaChoiceOptions
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 15;
    public Guid? FormulaId { get; init; }
    public Guid? OrderId { get; init; }
    public string? Keyword { get; init; }
}
