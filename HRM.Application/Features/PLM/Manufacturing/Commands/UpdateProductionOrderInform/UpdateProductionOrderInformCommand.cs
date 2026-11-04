using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.UpdateProductionOrderInform;

public sealed record UpdateProductionOrderInformCommand(Guid Id, ProductionOrderInformRequest Request) : IRequest<OperationResult>
{
    public string? ExpectedRevision { get; init; }
    public bool IsEditorSave { get; init; }
    public HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction.ProductionOrderEditorRequest? EditorPatch { get; init; }
}
