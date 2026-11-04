using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;

namespace HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;

/// <summary>Một cột lịch sử: ngày tạo QC, lô và snapshot kết quả đã lưu; không phải audit các lần PATCH.</summary>
public sealed record IqcEvaluationHistoryDto(Guid QCInputByQCId, long VoucherDetailId,
    DateTime QCCreatedDate, string? LotNumber, string MaterialExternalId,
    IqcEvaluationDto Evaluation, decimal QtyKg, string Unit);
