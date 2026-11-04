using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.CreateProductionOrderFormula;

public sealed record CreateProductionOrderFormulaCommand(Guid Id, CreateProductionOrderFormulaRequest Request) : IRequest<OperationResult<ProductionOrderFormulaCreatedDto>>
{
    public string? ExpectedRevision { get; init; }
}
