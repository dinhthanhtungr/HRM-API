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

public sealed record GetManufacturingFormulaVersionsQuery(
    Guid FormulaId,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<OperationResult<PagedResult<ManufacturingFormulaVersionDto>>>;
