using HRM.Domain.Enums.Attachment;
using HRM.Domain.Enums.Devandqa;

namespace HRM.Application.Features.DevAndQA.IQC.Dtos;

public class GetQCInputByQC
{
    // ----- Context: voucher detail đang được QC -----
    public long VoucherDetailId { get; set; }
    public string? MaterialExternalId { get; set; } // ProductCode
    public string? MaterialName { get; set; }       // ProductName
    public string? LotNumber { get; set; }
    public decimal? QtyKg { get; set; }
    public string? Unit { get; set; } = "kg";

    // ----- Supplier (nguồn “đúng”) -----
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? SupplierExternalId { get; set; }

    // ----- QC record (nullable: chưa QC) -----
    public Guid? QCInputByQCId { get; set; }
    public string? InspectionMethod { get; set; }
    public bool? IsCOAProvided { get; set; }
    public bool? IsMSDSTDSProvided { get; set; }
    public bool? IsMetalDetectionRequired { get; set; }
    public bool? IsSuccessQuality { get; set; } // Kiểm tra chất lượng đạt
    public Guid? AttachmentCollectionId { get; set; }
    public AttachmentUploadStatus AttachmentStatus { get; set; }
    public string? AttachmentLastError { get; set; }

    public QcDecision? ImportWarehouseType { get; set; }
    public string? Note { get; set; }

    // ----- QC audit -----
    public DateTime? QCCreatedDate { get; set; }
    public Guid? QCCreatedBy { get; set; }
    public string? QCEmployeeName { get; set; }
}
