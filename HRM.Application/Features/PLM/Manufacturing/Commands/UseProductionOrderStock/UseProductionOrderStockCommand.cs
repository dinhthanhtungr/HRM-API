using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.UseProductionOrderStock;

public sealed record UseProductionOrderStockCommand(Guid Id, UseProductionOrderStockRequest Request) : IRequest<OperationResult>
{
    public string? ExpectedRevision { get; init; }
    public Guid? SourceFormulaId { get; init; }
}
