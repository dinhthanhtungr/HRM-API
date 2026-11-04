namespace HRM.Application.Features.DevAndQA.LabTests.Dtos;

/// <summary>POST cần tên/ngày; PATCH bỏ field là giữ nguyên, xóa nullable qua ClearFields.</summary>
public sealed record LabTestWriteRequest
{
    public string? SampleName { get; init; }
    public DateOnly? TestDate { get; init; }
    public string? SupplierCustomerName { get; init; }
    public string? BatchNumber { get; init; }
    public string? ItemCode { get; init; }
    public string? Resin { get; init; }
    public string? Application { get; init; }
    public string? PreparedByName { get; init; }
    public string? Note { get; init; }
    public decimal? WeightKg { get; init; }
    public string? ImportedPhotoName { get; init; }
    public Guid? EvaluationTemplateId { get; init; }
    public IReadOnlyList<string>? ClearFields { get; init; }
    public IReadOnlyList<LabTestResultInput>? Results { get; init; }
}

public sealed record LabTestResultInput(Guid EvaluationStandardId, string? Value = null,
    bool ClearValue = false, string? ObservedText = null, bool ClearObservedText = false);
