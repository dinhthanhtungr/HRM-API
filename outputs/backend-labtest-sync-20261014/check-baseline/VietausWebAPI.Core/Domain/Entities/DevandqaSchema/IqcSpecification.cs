using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VietausWebAPI.Core.Domain.Entities.AttachmentSchema;
using VietausWebAPI.Core.Domain.Entities.CompanySchema;
using VietausWebAPI.Core.Domain.Entities.HrSchema;
using VietausWebAPI.Core.Domain.Entities.ManufacturingSchema;
using VietausWebAPI.Core.Domain.Entities.MaterialSchema;
using VietausWebAPI.Core.Domain.Entities.WarehouseSchema;
using VietausWebAPI.Core.Domain.Enums.Attachment;
using VietausWebAPI.Core.Domain.Enums.Devandqa;
using VietausWebAPI.Core.Domain.Enums.WareHouses;

namespace VietausWebAPI.Core.Domain.Entities.DevandqaSchema;

/// <summary>Bộ tiêu chuẩn IQC độc lập; mỗi bản ghi đại diện một phiên bản.</summary>
public class IqcSpecification
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid MaterialId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public IqcSpecificationStatus Status { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public ICollection<IqcSpecificationItem> Items { get; set; } = new List<IqcSpecificationItem>();
    public virtual Company Company { get; set; } = null!;
    public virtual Employee CreatedByEmployee { get; set; } = null!;
    public virtual Employee? ApprovedByEmployee { get; set; }
}
