using System.Text.Json;
using HRM.Domain.Enums.Materials;

namespace HRM.Application.Features.PLM.Materials.Dtos;

/// <summary>
/// Dữ liệu một phương án thay thế NVL. Giá không thuộc contract này để Lab có thể
/// xem phương án mà không mặc nhiên được xem giá.
/// </summary>
public sealed class MaterialReplacementDto
{
    public Guid MaterialReplacementId { get; init; }
    public Guid SourceMaterialId { get; init; }
    public Guid ReplacementMaterialId { get; init; }
    public string? ExternalId { get; init; }
    public string? CustomCode { get; init; }
    public string? Name { get; init; }
    public MaterialPurchaseStatus PurchaseStatus { get; init; }
    public bool IsPurchaseAvailable { get; init; }
    public JsonElement ApplicableContext { get; init; }
    public string? TechnicalNote { get; init; }
    public decimal? ReplacementRatio { get; init; }
    public int Priority { get; init; }
    public bool IsRecommended { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime? UpdatedDate { get; init; }
}

public sealed class CreateMaterialReplacementRequest
{
    public Guid ReplacementMaterialId { get; init; }
    public JsonElement? ApplicableContext { get; init; }
    public string? TechnicalNote { get; init; }
    public decimal? ReplacementRatio { get; init; }
    public int Priority { get; init; } = 1;
    public bool IsRecommended { get; init; }
}

/// <summary>
/// PUT thay toàn bộ dữ liệu nghiệp vụ của một phương án. TechnicalNote null nghĩa là xóa ghi chú.
/// </summary>
public sealed class UpdateMaterialReplacementRequest
{
    public Guid ReplacementMaterialId { get; init; }
    public JsonElement? ApplicableContext { get; init; }
    public string? TechnicalNote { get; init; }
    public decimal? ReplacementRatio { get; init; }
    public int Priority { get; init; } = 1;
    public bool IsRecommended { get; init; }
    public bool IsActive { get; init; } = true;
}

public enum MaterialReplacementWriteOutcome
{
    Created,
    Updated,
    NotFound,
    InvalidRequest
}

public sealed record MaterialReplacementWriteResult(
    MaterialReplacementWriteOutcome Outcome,
    MaterialReplacementDto? Data = null,
    string? Message = null);
