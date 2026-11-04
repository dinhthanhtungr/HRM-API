using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.InitializeManufacturingAdjustment;

public sealed record InitializeManufacturingAdjustmentQuery(Guid OrderId, Guid? AdjustmentId = null) : IRequest<OperationResult<ManufacturingAdjustmentEditor>>;
