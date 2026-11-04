using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.Devandqa;

namespace HRM.Domain.Entities.DevandqaSchema;

/// <summary>Bộ tiêu chuẩn COA độc lập; mỗi bản ghi đại diện một phiên bản.</summary>
public class CoaSpecification
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid ProductId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public CoaSpecificationStatus Status { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public ICollection<CoaSpecificationItem> Items { get; set; } = new List<CoaSpecificationItem>();
    public virtual Company Company { get; set; } = null!;
    public virtual Employee CreatedByEmployee { get; set; } = null!;
    public virtual Employee? ApprovedByEmployee { get; set; }
}
