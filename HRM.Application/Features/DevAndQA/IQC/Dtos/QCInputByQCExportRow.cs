namespace HRM.Application.Features.DevAndQA.IQC.Dtos;

public class QCInputByQCExportRow
{
    public string? MaterialExternalId { get; set; }   // Mã số
    public string? MaterialName { get; set; }         // Tên
    public string? ImportWarehouseText { get; set; }  // Nhập kho
    public string? SupplierName { get; set; }         // Nhà cung cấp
    public string? ResultText { get; set; }           // Kết quả
    public string? InspectorName { get; set; }        // Người kiểm
    public DateTime? InspectionDate { get; set; }     // Ngày kiểm
    public DateTime? CreatedDate { get; set; }        // Ngày nhập: ledger.CreatedAt mới nhất của dòng phiếu, null khi chưa phát sinh
    public string? LotNo { get; set; }                // Lot #
    public decimal? DeliveryQuantityKg { get; set; }  // Khối lượng giao hàng
    public decimal? ActualReceivedQuantityKg { get; set; } // Khối lượng thực nhận
    public string? Note { get; set; }                 // Ghi chú
}
