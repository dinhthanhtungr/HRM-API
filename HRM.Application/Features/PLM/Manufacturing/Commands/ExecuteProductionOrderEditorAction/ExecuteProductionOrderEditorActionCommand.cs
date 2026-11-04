using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction;

public sealed record ExecuteProductionOrderEditorActionCommand(Guid Id, ProductionOrderEditorRequest Request)
    : IRequest<OperationResult>;
