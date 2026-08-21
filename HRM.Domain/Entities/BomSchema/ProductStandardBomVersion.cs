using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Domain.Entities.BomSchema;

/// <summary>
/// Khoảng thời gian một phiên bản M-BOM được chọn làm BOM chuẩn của sản phẩm.
/// </summary>
public class ProductStandardBomVersion
{
    public Guid ProductStandardBomVersionId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid ProductId { get; set; }
    public Guid BomVersionId { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? ClosedDate { get; set; }
    public Guid? ClosedBy { get; set; }

    public virtual Product Product { get; set; } = null!;
    public virtual BomVersion BomVersion { get; set; } = null!;
    public virtual Company Company { get; set; } = null!;
}
