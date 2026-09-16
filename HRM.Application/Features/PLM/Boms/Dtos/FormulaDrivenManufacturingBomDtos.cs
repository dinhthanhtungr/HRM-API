using HRM.Domain.Enums.Boms;

namespace HRM.Application.Features.PLM.Boms.Dtos;

/// <summary>
/// Kết quả khởi tạo M-BOM Draft từ Formula đã được khách hàng chọn.
/// NVL là snapshot từ Formula; Process chỉ hoàn thiện công đoạn và hao hụt ở bước tiếp theo.
/// </summary>
public sealed class FormulaDrivenManufacturingBomDto
{
    public Guid FormulaId { get; init; }
    public string FormulaExternalId { get; init; } = string.Empty;
    public Guid BomDefinitionId { get; init; }
    public Guid BomVersionId { get; init; }
    public int VersionNo { get; init; }
    public BomVersionStatus Status { get; init; }
    public int ItemCount { get; init; }
    public bool IsExistingSnapshot { get; init; }
}
