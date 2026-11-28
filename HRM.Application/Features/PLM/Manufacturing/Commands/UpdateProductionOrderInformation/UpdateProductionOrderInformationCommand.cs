using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Commands.ApplyProductionOrderAction;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.UpdateProductionOrderInformation;

public sealed record UpdateProductionOrderInformationCommand(
    Guid Id,
    UpdateProductionOrderInformationRequest Request,
    ManufacturingInformationAction? Action = null
) : IRequest<OperationResult>
{
    public string? ExpectedRevision { get; init; }
    public Guid? SourceFormulaId { get; init; }
}
