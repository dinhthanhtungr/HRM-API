using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingAdjustmentGroups;

public sealed record GetManufacturingAdjustmentGroupsQuery(Guid Id, bool ByOrder = false) : IRequest<OperationResult<GetManufacturingFormulaAdjustmentBatchGroups>>;
