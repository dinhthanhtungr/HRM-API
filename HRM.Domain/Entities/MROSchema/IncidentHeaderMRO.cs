using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.MROSchema
{
    public class IncidentHeaderMRO
    {
        public long IncidentId { get; set; }                    // PK (bigint)
        public string IncidentCode { get; set; } = default!;      // ExternalId
        public string Status { get; set; } = default!;
        public string Title { get; set; } = default!;
        public string Description { get; set; }

        // Tham chiếu mềm (không FK)
        public int? EquipmentId { get; set; }                // 
        public string? EquipmentCode { get; set; }                // mã thiết bị ngoài

        public int? AreaId { get; set; }                // 
        public string? AreaCode { get; set; }                     // area_externalid


        // Thuộc về nhà máy (FK cứng)
        public Guid CompanyId { get; set; }


        public string? RolePrefix { get; set; }

        // Audit/tiến độ – FK nhân sự là OPTIONAL để vẫn lưu được khi chưa đồng bộ danh mục
        public DateTime CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }

        public DateTime? ExecAt { get; set; }
        public Guid? ExecBy { get; set; }


        public DateTime? DoneAt { get; set; }
        public Guid? DoneBy { get; set; }

        public DateTime? ClosedAt { get; set; }
        public Guid? ClosedBy { get; set; }

        public int? WaitMin { get; set; }
        public int? RepairMin { get; set; }
        public int? TotalMin { get; set; }

        public AreaMRO? Area { get; set; } = default!;
        public EquipmentMRO? Equipment { get; set; } = default!;
        public Company Company { get; set; } = default!;
        public Employee? CreatedByEmployee { get; set; }
        public Employee? ExecByEmployee { get; set; }
        public Employee? DoneByEmployee { get; set; }
        public Employee? ClosedByEmployee { get; set; }

        public virtual ICollection<StockOutHeaderMRO> StockOutHeaderMRO { get; set; } = new List<StockOutHeaderMRO>();

    }
}
