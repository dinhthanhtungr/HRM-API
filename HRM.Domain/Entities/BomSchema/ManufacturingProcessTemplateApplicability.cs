using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Domain.Entities.BomSchema;

/// <summary>
/// Quy tắc xác định loại sản phẩm và tuyến sản xuất phù hợp với một phiên bản Process Template.
/// </summary>
public sealed class ManufacturingProcessTemplateApplicability
{
    public Guid ManufacturingProcessTemplateApplicabilityId { get; set; }
    public Guid ManufacturingProcessTemplateId { get; set; }
    public Guid? CategoryId { get; set; }
    public StepOfProduct? StepOfProduct { get; set; }
    public int Priority { get; set; }
    public string? Note { get; set; }

    public ManufacturingProcessTemplate ProcessTemplate { get; set; } = null!;
    public Category? Category { get; set; }
}
