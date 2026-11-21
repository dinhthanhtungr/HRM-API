using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.Formulas;
namespace HRM.Application.Features.PLM.MaterialProcessing.Dtos;

public sealed record MaterialProcessingLineRequest(Guid MaterialId, decimal QuantityKg, string? LotNo = null, ItemType ItemType = ItemType.Material);
public sealed record ProcessingStockLotDto(string LotNo, decimal QuantityKg);
public sealed record ProcessingStockDto(decimal MaxQuantityKg, IReadOnlyList<ProcessingStockLotDto> Lots);
public sealed record SaveMaterialProcessingRequest(
    string ProcessingType, Guid MaterialId, decimal TotalQuantityRequest, int? NumOfBatches,
    StepOfProduct? StepOfProduct, DateTime? ManufacturingDate, string Status,
    string? Note, List<MaterialProcessingLineRequest> Details, int ExpectedRevision = 0, string? QcCheck = null);
public sealed record ConfirmMaterialProcessingRequest(int ExpectedRevision);
public sealed record MaterialProcessingLineDto(Guid DetailId, Guid MaterialId, int LineNo,
    string Code, string Name, decimal QuantityKg, decimal Percentage,
    Guid? CategoryId = null, string? Unit = null, decimal QuantityPerBatch = 0, string? LotNo = null, ItemType ItemType = ItemType.Material);
public sealed record MaterialProcessingDto(Guid Id, string ExternalId, string ProcessingType,
    Guid MaterialId, string MaterialCode, string MaterialName, decimal TotalQuantityRequest,
    int? NumOfBatches, StepOfProduct? StepOfProduct, DateTime? ManufacturingDate,
    string Status, string? Note, int Revision, DateTime CreatedDate, DateTime UpdatedDate,
    IReadOnlyList<MaterialProcessingLineDto> Details)
{
    public decimal QuantityPerBatch => decimal.Round(TotalQuantityRequest / (NumOfBatches ?? 1), 3);
    public ProcessingMaterialOption? BaseMaterial { get; init; }
    public string? QcCheck { get; init; }
}
public sealed record MaterialProcessingSummaryDto(Guid Id, string ExternalId, string ProcessingType,
    Guid MaterialId, string MaterialCode, string MaterialName, decimal TotalQuantityRequest,
    int? NumOfBatches, StepOfProduct? StepOfProduct, DateTime? ManufacturingDate,
    int LineCount, string Status, int Revision, DateTime CreatedDate, DateTime UpdatedDate);
public sealed record MaterialProcessingListDto(IReadOnlyList<MaterialProcessingSummaryDto> Items, int TotalCount, int PageNumber);
public sealed record ProcessingMaterialOption(Guid MaterialId, string Code, string Name,
    Guid? BaseMaterialId = null, string? BaseCode = null, string? BaseName = null,
    string? ProcessingType = null, bool IsPreview = false, Guid? CategoryId = null, string? Unit = null);
public sealed record MaterialProcessingResult(string Code, MaterialProcessingDto? Data = null, string? Message = null);

public sealed class PatchMaterialProcessingRequest
{
    public int ExpectedRevision { get; init; }
    public string? ProcessingType { get; init; }
    public Guid? MaterialId { get; init; }
    public decimal? TotalQuantityRequest { get; init; }
    public int? NumOfBatches { get; init; }
    public HRM.Domain.Enums.Manufacturings.StepOfProduct? StepOfProduct { get; init; }
    public DateTime? ManufacturingDate { get; init; }
    public string? Status { get; init; }
    public string? Note { get; init; }
    public string? QcCheck { get; init; }
    public List<MaterialProcessingLineRequest>? Details { get; init; }
    public List<string>? ClearFields { get; init; }
}
