using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingAdjustment;

public sealed record GetManufacturingAdjustmentQuery(Guid Id) : IRequest<OperationResult<GetManufacturingFormulaAdjustment>>;
