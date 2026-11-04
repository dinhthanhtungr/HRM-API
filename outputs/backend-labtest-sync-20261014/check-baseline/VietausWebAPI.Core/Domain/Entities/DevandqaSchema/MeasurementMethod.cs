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

/// <summary>Danh mục phương pháp đo theo công ty, dùng để chọn vào chỉ tiêu và template.</summary>
public class MeasurementMethod
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Tên tiếng Anh tùy chọn; chưa có thì dùng Name.</summary>
    public string? NameEn { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public Company Company { get; set; } = null!;
    public Employee CreatedByEmployee { get; set; } = null!;
    public Employee? UpdatedByEmployee { get; set; }
}