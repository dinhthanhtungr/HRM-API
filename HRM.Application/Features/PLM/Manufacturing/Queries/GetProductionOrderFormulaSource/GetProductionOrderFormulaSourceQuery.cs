using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrderFormulaSource;

public sealed record GetProductionOrderFormulaSourceQuery(Guid Id, Guid FormulaId) : IRequest<OperationResult<ProductionOrderFormulaSourceDto>>;
