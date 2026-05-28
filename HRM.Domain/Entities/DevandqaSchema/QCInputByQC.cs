using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.Attachment;
using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Domain.Entities.DevandqaSchema
{
    public class QCInputByQC
    {
        public Guid QCInputByQCId { get; set; }
        public Guid? AttachmentCollectionId { get; set; }
        public long VoucherDetailId { get; set; }

        public string? CSName { get; set; }
        public string? CSExternalId { get; set; }

        public string? MaterialName { get; set; }
        public string? MaterialExternalId { get; set; }


        public string? InspectionMethod { get; set; }// Ví dụ: "Ngoại quan", "Test lab", "Thổi màng", ...
        public bool? IsCOAProvided { get; set; } // Kiểm tra COA
        public bool? IsMSDSTDSProvided { get; set; } // Kiểm tra MSDS/TDS
        public bool? IsMetalDetectionRequired { get; set; } // Kiểm tra phát hiện kim loại
        public bool? IsSuccessQuality { get; set; } // Kiểm tra chất lượng đạt

        public QcDecision? ImportWarehouseType { get; set; } // loại kho nhập
        public string? Note { get; set; } // Ghi chú

        public DateTime CreatedDate { get; set; }
        public Guid CreatedBy { get; set; }

        public AttachmentUploadStatus AttachmentStatus { get; set; } = AttachmentUploadStatus.None;
        public string? AttachmentLastError { get; set; }


        public virtual WarehouseVoucherDetail WarehouseVoucherDetail { get; set; } = null!;
        public virtual Employee? Employee { get; set; }
        public virtual AttachmentCollection? AttachmentCollection { get; set; }

    }
}
