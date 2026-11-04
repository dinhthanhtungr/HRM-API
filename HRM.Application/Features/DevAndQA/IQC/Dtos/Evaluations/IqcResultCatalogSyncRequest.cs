namespace HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;

/// <summary>Current: một QC ID; Selected: danh sách QC ID; Company: company hiện tại. Codes hoặc AllCriteria, không dùng đồng thời.</summary>
public sealed record IqcResultCatalogSyncRequest(string Scope, IReadOnlyList<Guid>? QcInputByQCIds = null,
    IReadOnlyList<string>? Codes = null, bool AllCriteria = false);
