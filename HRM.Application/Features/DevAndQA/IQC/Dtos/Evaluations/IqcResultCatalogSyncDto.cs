using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;

namespace HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;

public sealed record IqcResultCatalogSyncChangeDto(Guid QcInputByQCId, EvaluationResultDto Before, EvaluationResultDto After);
public sealed record IqcResultCatalogSyncIssueDto(Guid QcInputByQCId, Guid ResultId, string Code, string Message);
/// <summary>Counts cho toàn bộ phạm vi; Changes/Issues giới hạn 200 mẫu, token kiểm tra dữ liệu giữa preview và apply.</summary>
public sealed record IqcResultCatalogSyncDto(string PreviewToken, bool CanApply, bool Applied, int MatchedQcCount,
    int MatchedResultCount, int ChangedQcCount, int ChangedResultCount, int IssueCount,
    IReadOnlyList<IqcResultCatalogSyncChangeDto> Changes, IReadOnlyList<IqcResultCatalogSyncIssueDto> Issues,
    bool ChangesTruncated, bool IssuesTruncated);
