using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.CalculateManufacturingAdjustment;

public sealed record CalculateManufacturingAdjustmentQuery(Guid OrderId, ManufacturingAdjustmentInput Request, Guid? AdjustmentId = null) : IRequest<OperationResult<ManufacturingAdjustmentCalculation>>;
