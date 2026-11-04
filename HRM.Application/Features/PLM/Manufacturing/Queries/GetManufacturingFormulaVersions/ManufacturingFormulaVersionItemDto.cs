using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Domain.Enums.Formulas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulaVersions;

public sealed record ManufacturingFormulaVersionItemDto(
    Guid Id,
    Guid ItemId,
    ItemType ItemType,
    Guid CategoryId,
    int LineNo,
    decimal Quantity,
    decimal? UnitPrice,
    decimal? TotalPrice,
    string? Name,
    string? Code,
    string? Unit
);
