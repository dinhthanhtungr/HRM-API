namespace HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;
/// <summary>Một phiếu QC đã lưu, làm một cột kết quả Excel.</summary>
public sealed record IqcEvaluationExportColumn(DateTime QCCreatedDate, string ProductCode,
    string ProductName, string? LotNumber, IqcEvaluationDto Evaluation);
