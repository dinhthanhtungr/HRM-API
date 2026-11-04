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

/// <summary>Danh mục chỉ tiêu theo công ty dùng cho dropdown IQC/COA; cấu hình là mặc định để sao chép.</summary>
public class EvaluationStandard
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Tên tiếng Anh tùy chọn; chưa có thì dùng Name.</summary>
    public string? NameEn { get; set; }
    public string? Description { get; set; }
    public EvaluationDataType DataType { get; set; } = EvaluationDataType.Text;
    public string? Unit { get; set; }
    public Guid? MeasurementMethodId { get; set; }
    public MeasurementMethod? MeasurementMethod { get; set; }
    public string? TestMethod { get; set; }
    public string? InputConfigJson { get; set; }
    public string? AcceptanceRuleJson { get; set; }
    public string? SpecificationText { get; set; }
    public bool IsRequired { get; set; }
    public Guid CompanyId { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public Guid? UpdatedBy { get; set; }


    public virtual Company Company { get; set; } = null!;
    public virtual Employee CreatedByEmployee { get; set; } = null!;
    public virtual Employee? UpdatedByEmployee { get; set; }
}
