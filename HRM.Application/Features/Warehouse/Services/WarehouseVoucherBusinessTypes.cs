using HRM.Application.Features.Warehouse.Dtos;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Features.Warehouse.Services;

internal static class WarehouseVoucherBusinessTypes
{
    public static WarehouseVoucherBusinessTypeDto Create(string code, string vi, string en) =>
        new(code, vi, en, code switch
        {
            "MixingReceipt" or "MixingIssue" or "MixingProductionIssue" or "ColorAdjustmentIssue" => "primary",
            "ImportDefectiveFinishedGood" or "ExportDefectiveFinishedGood" or
            "ImportDefectiveRawMaterial" or "ExportDefectiveRawMaterial" or
            "DeliveryDefectiveGoodsIssue" or "ProductionDefectiveReceipt" => "danger",
            "ExportFinishedGood" or "ExportRawMaterial" or "ExportOther" or "ExportReturn" or "ExportMaterial" or
            "PurchaseReturn" or "DeliveryGoodsIssue" or "ProductionMaterialIssue" => "warning",
            "ImportFinishedGood" or "ImportRawMaterial" or "ImportOther" or "ImportReturn" or "ImportMaterial" or
            "PurchaseGoodsReceipt" or "PurchaseSuppliesReceipt" or "PurchaseRawMaterialReceipt" or "ProductionReceipt" => "success",
            _ => "muted"
        });

    public static WarehouseVoucherBusinessTypeDto Fallback(int type) => (WareHouseRequestType)type switch
    {
        WareHouseRequestType.ImportFinishedGood => Create("ImportFinishedGood", "Nhập thành phẩm", "Finished goods receipt"),
        WareHouseRequestType.ExportFinishedGood => Create("ExportFinishedGood", "Xuất thành phẩm", "Finished goods issue"),
        WareHouseRequestType.ImportDefectiveFinishedGood => Create("ImportDefectiveFinishedGood", "Nhập thành phẩm lỗi", "Defective finished goods receipt"),
        WareHouseRequestType.ExportDefectiveFinishedGood => Create("ExportDefectiveFinishedGood", "Xuất thành phẩm lỗi", "Defective finished goods issue"),
        WareHouseRequestType.ImportRawMaterial => Create("ImportRawMaterial", "Nhập nguyên vật liệu", "Raw material receipt"),
        WareHouseRequestType.ExportRawMaterial => Create("ExportRawMaterial", "Xuất nguyên vật liệu", "Raw material issue"),
        WareHouseRequestType.ImportDefectiveRawMaterial => Create("ImportDefectiveRawMaterial", "Nhập nguyên vật liệu lỗi", "Defective raw material receipt"),
        WareHouseRequestType.ExportDefectiveRawMaterial => Create("ExportDefectiveRawMaterial", "Xuất nguyên vật liệu lỗi", "Defective raw material issue"),
        WareHouseRequestType.ImportOther => Create("ImportOther", "Nhập khác", "Other receipt"),
        WareHouseRequestType.ExportOther => Create("ExportOther", "Xuất khác", "Other issue"),
        WareHouseRequestType.ImportReturn => Create("ImportReturn", "Nhập hàng trả về", "Returned goods receipt"),
        WareHouseRequestType.ExportReturn => Create("ExportReturn", "Xuất trả nhà cung cấp", "Return to supplier"),
        WareHouseRequestType.ImportMaterial => Create("ImportMaterial", "Nhập vật tư", "Supplies receipt"),
        WareHouseRequestType.ExportMaterial => Create("ExportMaterial", "Xuất vật tư", "Supplies issue"),
        _ => Create("Unknown", $"Loại phiếu khác — mã {type}", $"Other voucher type — code {type}")
    };
}
