using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrderFormulaSource;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrderWorkspace;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrderInform;

public sealed record GetProductionOrderInformQuery(Guid Id) : IRequest<OperationResult<ProductionOrderInformDto>>;
