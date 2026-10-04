using HRM.Application.Features.PLM.Formulas.Dtos.GetFormulas;

namespace HRM.Application.Features.PLM.SampleRequests.Dtos.FormulaOptions;

public sealed class SampleRequestFormulaOptionsDto
{
    public SampleRequestFormulaOptionsHeaderDto Header { get; init; } = new();
    public IReadOnlyList<FormulaId> FormulaSelects { get; init; } = [];
    public IReadOnlyList<FormulaId> FormulaDevs { get; init; } = [];
    public IReadOnlyList<FormulaId> FormulaStandard { get; init; } = [];
}

public sealed class SampleRequestFormulaOptionsHeaderDto
{
    public Guid SampleRequestId { get; init; }
    public string SampleRequestExternalId { get; init; } = string.Empty;
    public Guid ProductId { get; init; }
    public string? ColourCode { get; init; }
    public string? SaleNote { get; init; }
    public string? LabNote { get; init; }
    public string? SpecialRequirement { get; init; }
    public string? Requirement { get; init; }
}
