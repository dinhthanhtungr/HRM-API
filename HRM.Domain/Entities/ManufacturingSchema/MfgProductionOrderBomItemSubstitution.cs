using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Boms;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Domain.Entities.ManufacturingSchema;

/// <summary>
/// Nhật ký thay thế đầu vào thực tế của một dòng BOM trên lệnh sản xuất.
/// Không dùng để thay thế batch/lot hoặc máy/công đoạn.
/// </summary>
public class MfgProductionOrderBomItemSubstitution
{
    public Guid MfgProductionOrderBomItemSubstitutionId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid MfgProductionOrderId { get; set; }
    public Guid BomVersionItemId { get; set; }
    public Guid? BomVersionItemAlternativeId { get; set; }
    public BomVersionItemAlternativeType SourceItemTypeSnapshot { get; set; }
    public string? SourceItemCodeSnapshot { get; set; }
    public string? SourceItemNameSnapshot { get; set; }
    public BomVersionItemAlternativeType ActualItemType { get; set; }
    public Guid? ActualMaterialId { get; set; }
    public Guid? ActualComponentProductId { get; set; }
    public decimal PlannedQuantity { get; set; }
    public decimal ActualQuantity { get; set; }
    public decimal AppliedRatio { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Note { get; set; }
    public MfgProductionOrderBomItemSubstitutionStatus Status { get; set; } = MfgProductionOrderBomItemSubstitutionStatus.Pending;
    public DateTime RequestedDate { get; set; }
    public Guid RequestedBy { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? AppliedDate { get; set; }
    public Guid? AppliedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public Guid? UpdatedBy { get; set; }

    public virtual Company Company { get; set; } = null!;
    public virtual MfgProductionOrder ProductionOrder { get; set; } = null!;
    public virtual BomVersionItem BomVersionItem { get; set; } = null!;
    public virtual BomVersionItemAlternative? BomVersionItemAlternative { get; set; }
    public virtual Material? ActualMaterial { get; set; }
    public virtual Product? ActualComponentProduct { get; set; }
}
