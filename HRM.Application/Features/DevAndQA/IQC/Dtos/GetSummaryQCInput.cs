using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Features.DevAndQA.IQC.Dtos;

public class GetSummaryQCInput
{
    /// <summary>Warehouse hoặc LabTest. LabTest không có voucher (VoucherDetailId=0).</summary>
    public string Source { get; set; } = "Warehouse";
    public Guid? LabTestId { get; set; }
    public DateOnly? TestDate { get; set; }
    /// <summary>Khối lượng mẫu test nhập tay; không phải tồn kho. Null nếu chưa nhập.</summary>
    public decimal? WeightKg { get; set; }
    public long VoucherDetailId { get; set; }

    public string ExternalId { get; set; } = string.Empty; // ProductCode
    public string Name { get; set; } = string.Empty;       // ProductName

    public string LotNumber { get; set; } = string.Empty;
    public decimal QtyKg { get; set; }
    public string Unit { get; set; } = "kg";

    public string CategoryName { get; set; } = string.Empty; // dùng để hiển thị, lấy từ WarehouseVoucherDetail.ProductCode -> map sang category name

    public VoucherDetailType VoucherType { get; set; }
    public string RequestCode { get; set; } = string.Empty;

    // QC summary
    public bool HasQC { get; set; }
    public Guid? QCInputByQCId { get; set; }      // nullable cho đúng nghĩa "chưa QC"
    public string QCEmployeeName { get; set; } = string.Empty;
    public DateTime? QCCreatedDate { get; set; } // nullable
    /// <summary>Ledger.CreatedAt mới nhất của dòng phiếu; null nếu chưa có ledger, không phải ngày tạo phiếu kho.</summary>
    public DateTime? InputDate { get; set; }
    public string? QCDescription { get; set; }
}
