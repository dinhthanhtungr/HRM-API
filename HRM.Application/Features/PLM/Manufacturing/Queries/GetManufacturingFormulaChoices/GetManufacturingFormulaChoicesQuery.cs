using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulaChoices;

public sealed record GetManufacturingFormulaChoicesQuery(ManufacturingFormulaChoiceOptions Options) : IRequest<OperationResult<PagedResult<ManufacturingFormulaChoiceDto>>>;
