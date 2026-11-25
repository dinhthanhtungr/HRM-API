namespace HRM.Application.Features.Reports.PLPU;

public sealed record PLPUMaterialActivityDto(
    PLPUMaterialActivityRow Total,
    IReadOnlyList<PLPUMaterialActivityRow> Months);

public sealed record PLPUMaterialActivityRow(
    DateTime From,
    DateTime To,
    PLPUMaterialActivityCounts Current,
    PLPUMaterialActivityCounts? PriorYear);

public sealed record PLPUMaterialActivityCounts(
    int ReceivedCodes,
    decimal ReceivedKg,
    int ReturnedDefectiveCodes,
    int SpecialAcceptanceCodes);
