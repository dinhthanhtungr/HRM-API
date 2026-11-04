using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.ManufacturingSchema;

// Mapping các cột legacy đã tồn tại trong DB, được user xác nhận; không tạo schema.
public partial class MfgProductionOrder
{
    public bool? IsPrintedStock { get; set; } = false;
    public Guid? Checker { get; set; }
    public DateTime? CheckedDate { get; set; }
    public Employee? CheckerNavigation { get; set; }
}
