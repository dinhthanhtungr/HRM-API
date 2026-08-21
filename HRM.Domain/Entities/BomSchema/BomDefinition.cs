using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Boms;

namespace HRM.Domain.Entities.BomSchema;

/// <summary>
/// Header ổn định của một E-BOM hoặc M-BOM thuộc sản phẩm và công ty.
/// </summary>
public class BomDefinition
{
    public Guid BomDefinitionId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid ProductId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public BomType BomType { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public Guid? UpdatedBy { get; set; }

    public virtual Product Product { get; set; } = null!;
    public virtual Company Company { get; set; } = null!;
    public virtual ICollection<BomVersion> Versions { get; set; } = new List<BomVersion>();
}
