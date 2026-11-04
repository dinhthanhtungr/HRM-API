using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.ListManufacturingAdjustments;

public sealed record ListManufacturingAdjustmentsQuery(
    Guid? OrderId = null,
    Guid? FormulaId = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<OperationResult<List<GetManufacturingFormulaAdjustment>>>
{
    public int? BatchNo { get; init; }
    public int? TrialNo { get; init; }
    public string? BatchStatus { get; init; }
    public bool? IsAdditionalBatch { get; init; }
    public bool IncludeInactive { get; init; }
}
