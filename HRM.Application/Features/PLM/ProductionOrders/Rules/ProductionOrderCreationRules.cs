using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Features.PLM.ProductionOrders.Rules;

internal static class ProductionOrderCreationRules
{
    public const decimal FormulaRatioTolerance = 0.0001m;

    public static string? ValidateInternal(CreateInternalProductionOrderRequest? request)
    {
        if (request is null) return "Dữ liệu không hợp lệ.";
        if (request.ProductId == Guid.Empty) return "ProductId không hợp lệ.";
        if (request.TotalQuantityRequest <= 0) return "TotalQuantityRequest phải > 0.";
        if (string.IsNullOrWhiteSpace(request.BagType)) return "BagType không được rỗng.";
        if (request.StepOfProduct.HasValue && !Enum.IsDefined(request.StepOfProduct.Value))
            return "StepOfProduct không hợp lệ.";
        var status = request.InitialStatus?.Trim();
        if (!string.IsNullOrEmpty(status) &&
            !Enum.GetNames<ManufacturingProductOrder>().Contains(status, StringComparer.Ordinal))
            return "InitialStatus không hợp lệ.";
        return null;
    }

    public static string? ValidateInform(CreateProductionOrderInformRequest? request)
    {
        if (request is null) return "Dữ liệu không hợp lệ.";
        if (request.MerchandiseOrderId == Guid.Empty) return "MerchandiseOrderId không hợp lệ.";
        if (request.MerchandiseOrderDetailId == Guid.Empty) return "MerchandiseOrderDetailId không hợp lệ.";
        if (!request.ProductId.HasValue || request.ProductId == Guid.Empty) return "ProductId không hợp lệ.";
        if (request.RequiredDate == default) return "RequiredDate không hợp lệ.";
        if (request.StepOfProduct.HasValue && !Enum.IsDefined(request.StepOfProduct.Value))
            return "StepOfProduct không hợp lệ.";
        if (request.FormulaItems is null || request.FormulaItems.Any(x => x is null))
            return "Danh sách công thức không hợp lệ.";
        var items = ActiveItems(request);
        if (items.Any(x => !Enum.IsDefined(x.ItemType) ||
                           (x.LotNumber is not null && !Enum.IsDefined(x.LotNumber.StockType))))
            return "Loại vật tư hoặc tồn kho không hợp lệ.";
        if (items.Count > 0 && Math.Abs(items.Sum(x => x.Quantity) - 1m) > FormulaRatioTolerance)
            return "Tổng tỷ lệ công thức phải bằng 1.";
        return null;
    }

    public static List<CreateProductionOrderFormulaItemRequest> ActiveItems(CreateProductionOrderInformRequest request)
        => request.FormulaItems.Where(x => x.IsActive && x.Quantity > 0).OrderBy(x => x.LineNo).ToList();

    public static bool IsMaterial(ItemType itemType) => itemType is ItemType.Material or ItemType.MaterialFailure;

    public static string? NormalizeLotNumber(ProductionOrderLotNumberDto? lotNumber)
    {
        var lot = lotNumber?.LotNo?.Trim();
        return string.IsNullOrEmpty(lot) || string.Equals(lot, "N/A", StringComparison.OrdinalIgnoreCase) ? null : lot;
    }

    public static ItemType ResolveItemType(ItemType itemType, ProductionOrderLotNumberDto? lotNumber)
    {
        var defective = lotNumber?.StockType is StockType.DefectiveFinishedGood or StockType.DefectiveRawMaterial;
        return IsMaterial(itemType)
            ? defective ? ItemType.MaterialFailure : ItemType.Material
            : defective ? ItemType.ProductFailure : ItemType.Product;
    }
}
