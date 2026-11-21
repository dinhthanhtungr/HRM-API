namespace HRM.Application.Features.Warehouse.Dtos;

/// <summary>Reason recorded at import confirmation, not a QC diagnosis or a remaining-stock allocation.</summary>
public sealed record DefectReasonDto(string Reason, string SourceKind, string RequestCode, string VoucherCode, DateTime? RecordedAt);
