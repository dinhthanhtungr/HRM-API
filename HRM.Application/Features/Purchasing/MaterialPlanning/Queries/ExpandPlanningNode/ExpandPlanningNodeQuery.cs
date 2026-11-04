using System.ComponentModel.DataAnnotations;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.MaterialPlanning.Dtos;
using MediatR;

namespace HRM.Application.Features.Purchasing.MaterialPlanning.Queries.ExpandPlanningNode;

public sealed class ExpandPlanningNodeQuery : IRequest<OperationResult<PlanningExpansionDto>>
{
    public Guid OrderId { get; set; }
    public Guid LineId { get; set; }
    public Guid? ProductionOrderId { get; init; }
    // IDs của các dòng ManufacturingFormulaMaterial (VA) hoặc FormulaMaterial (VU) từ gốc tới Product cần mở.
    [MaxLength(10)]
    public Guid[] Path { get; init; } = [];
}
