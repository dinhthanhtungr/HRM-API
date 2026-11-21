using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrderFormulaSource;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrderWorkspace;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrderInform;

public sealed record ProductionOrderInformDto(ProductionOrderWorkspaceDto Workspace, ProductionOrderFormulaSourceDto? SelectedFormula)
{
    public string? PoNo { get; init; }
    public ProductionOrderSampleInfoDto? SampleInfo { get; init; }
    public string Revision { get; init; } = string.Empty;
    public IReadOnlyList<string> AllowedActions { get; init; } = [];
    public IReadOnlyList<string> EditableFields { get; init; } = [];
    public bool CanEditItems { get; init; }
    public string? FormulaEditLockReason { get; init; }
    public string? ReadOnlyReason { get; init; }
    public IReadOnlyList<HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders.ManufacturingStepOption> StepOptions { get; init; } = [];
    public IReadOnlyList<ProductionOrderFormulaSourceDto> FormulaSources { get; init; } = [];
}

public sealed record ProductionOrderSampleInfoDto(
    Guid ProductId, Guid? SampleRequestId, string? ExternalId,
    double? UsageRate, string? ExpiryType);
