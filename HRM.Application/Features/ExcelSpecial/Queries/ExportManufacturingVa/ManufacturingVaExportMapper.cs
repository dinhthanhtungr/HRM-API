using HRM.Application.Features.PLM.Shared.Rules;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.Merchadises;

namespace HRM.Application.Features.ExcelSpecial.Queries.ExportManufacturingVa;

internal static class ManufacturingVaExportMapper
{
    internal static IReadOnlyList<ManufacturingVaExcelRow> Map(
        IReadOnlyList<VaHeader> headers, IReadOnlyList<VaMaterial> materials,
        IReadOnlyList<VaSelection> selections, IReadOnlyList<VaOrder> orders,
        IReadOnlyList<VaProductCode> productCodes, IReadOnlyList<VaReceipt> receipts, CancellationToken ct)
    {
        var itemsByVa = materials.ToLookup(x => x.VaId);
        var selectionsByVa = selections.ToLookup(x => x.VaId);
        var ordersByMfg = orders.ToLookup(x => x.MfgId);
        var codesByProduct = productCodes.ToLookup(x => x.ProductId);
        var receiptByLot = receipts.ToDictionary(x => x.Lot, StringComparer.Ordinal);
        var result = new List<ManufacturingVaExcelRow>(headers.Count);
        foreach (var header in headers)
        {
            ct.ThrowIfCancellationRequested();
            var notes = new List<string>();
            var items = itemsByVa[header.Id].OrderBy(x => x.LineNo).ThenBy(x => x.Id).ToArray();
            var selected = selectionsByVa[header.Id].ToArray();
            var current = selected.Where(x => x.ValidTo is null).ToArray();
            var distinct = (current.Length > 0 ? current : selected).DistinctBy(x => x.MfgId).ToArray();
            var mfg = distinct.Length == 1 ? distinct[0] : null;
            if (mfg is null) notes.Add("Không có lệnh MFG duy nhất");
            else if (current.Length == 0) notes.Add("Liên kết MFG lịch sử");
            if (mfg is not null && mfg.PlannedQuantity is null) notes.Add("Lệnh MFG chưa có khối lượng sản xuất");
            var linkedOrders = mfg is null ? [] : ordersByMfg[mfg.MfgId].Distinct().ToArray();
            if (linkedOrders.Length == 0) notes.Add("Không tìm thấy đơn hàng liên kết");
            var customers = Join(linkedOrders.Select(x => x.CustomerCode));
            if (customers.Length == 0) notes.Add("Thiếu mã KH từ đơn hàng");
            var outputs = linkedOrders.Select(Output).Distinct().ToArray();
            if (outputs.Length > 1) notes.Add("Nhiều loại đầu ra");
            var isSingle = items.Length == 1 && items[0].Type is ItemType.Material or ItemType.MaterialFailure
                && items[0].MaterialId.HasValue && !items[0].ProductId.HasValue;
            var classification = Classify(isSingle, mfg?.Step, mfg?.CategoryCode);
            if (isSingle && mfg?.Step is { } step && step != StepOfProduct.Forsb)
                notes.Add("TH1/TM theo quy tắc 1 NVL đã chốt, dù có công đoạn SX/GC");
            if (isSingle && items[0].Type == ItemType.MaterialFailure) notes.Add("Đầu vào NVL lỗi");
            if (items.Length == 0) notes.Add("Thiếu chi tiết công thức");
            if (items.Any(x => !IsKg(x.Unit))) notes.Add("ĐVT nguồn thiếu/khác kg; ĐVT đặt Kg theo yêu cầu, chưa quy đổi");
            if (items.Any(x => x.Quantity == 0)) notes.Add("Có dòng công thức định mức bằng 0");
            var codes = mfg is null ? [] : codesByProduct[mfg.ProductId].Select(x => x.Code).Distinct().ToArray();
            if (codes.Length > 1) notes.Add("Nhiều SampleRequest cho Product");
            receiptByLot.TryGetValue(header.Code, out var receipt);
            if (receipt is null) notes.Add("Không có phiếu nhập TP đạt hợp lệ theo lot VA");
            else if (!string.IsNullOrEmpty(mfg?.ColourCode) && receipt.ProductCodes.Any(x => x != mfg.ColourCode))
                notes.Add("Mã hàng phiếu kho khác mã hàng Product - kiểm tra");
            if (mfg?.Step is null) notes.Add("Thiếu công đoạn");
            result.Add(new(header.Code, header.CreatedDate, receipt?.LastDate,
                Join(linkedOrders.Select(x => x.Code)), customers, mfg?.MfgCode ?? string.Empty,
                classification.Case, classification.Type, Join(outputs), mfg?.Status ?? string.Empty,
                Join(codes), mfg?.ColourCode, mfg?.ProductName, mfg?.PlannedQuantity, receipt?.Quantity,
                isSingle, Operation(mfg?.Step), mfg?.CategoryName, string.Join("; ", notes),
                items.Select(x => new ManufacturingVaExcelItem(x.Code, x.Name, x.Quantity)).ToArray()));
        }
        return result;
    }

    private static bool IsKg(string? unit) => unit?.Trim().ToUpperInvariant() is "KG" or "KILOGRAM";
    private static string Join(IEnumerable<string?> values) => string.Join("; ", values
        .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x, StringComparer.Ordinal));

    private static string Output(VaOrder order)
    {
        if (order.Type == OrderType.Internal || PLMCustomerRules.IsInternalCustomerExternalId(order.CustomerCode)) return "Nội bộ";
        return order.Type switch
        {
            OrderType.SampleRequest => "Đơn mẫu cho khách",
            OrderType.Merchandise => "Đơn bán cho khách",
            OrderType.Complaint => "Đơn khiếu nại - cần xác nhận",
            _ => "Chưa xác định"
        };
    }

    internal static (string Case, string Type) Classify(bool singleMaterial, StepOfProduct? step, string? category)
    {
        // Quy tắc người dùng đã chốt: đúng một NVL ưu tiên hơn mọi công đoạn/nhóm TP.
        if (singleMaterial) return ("TH1: Hàng thương mại", "TM");
        if (step == StepOfProduct.BN_GH) return ("TH6: Nghiền", "SX");
        if (step is StepOfProduct.CT_MD_HT_GH or StepOfProduct.MD_HT_GH or StepOfProduct.CT_HT_GH)
            return ("TH4: Sản xuất có khâu hoàn thiện", "SX");
        if (step == StepOfProduct.BTS_GH) return ("Ngoài TH1-TH6: Băm gia công", "GC");
        if (category == "GCO") return ("Gia công - cần xác nhận TH", "GC");
        if (category is "PIG" or "ADD" or "PPG" or "PBM") return ("TH2: Sản xuất bột màu/phụ gia (đề xuất)", "SX");
        if (step is StepOfProduct.CT_GH or StepOfProduct.HT_GH) return ("TH5: Trộn pre-colour (đề xuất)", "SX");
        if (step is StepOfProduct.MD_GH or StepOfProduct.CT_MD_GH or StepOfProduct.MDTS_GH or StepOfProduct.CT_MDTS_GH)
            return ("TH3: Sản xuất hạt nhựa", "SX");
        if (category is "CMB" or "AMB" or "CMP" or "PHM" or "PC" or "PMA" or "VRG")
            return ("TH3: Sản xuất hạt nhựa (theo nhóm TP)", "SX");
        return ("Chưa đủ căn cứ", string.Empty);
    }

    private static string Operation(StepOfProduct? step) => step switch
    {
        StepOfProduct.Forsb => "Sang bao", StepOfProduct.MD_GH => "Đùn", StepOfProduct.CT_GH => "Trộn",
        StepOfProduct.HT_GH => "Trộn Recolor", StepOfProduct.BN_GH => "Nghiền",
        StepOfProduct.CT_MD_GH => "Trộn → Đùn", StepOfProduct.MDTS_GH => "Đùn tái sinh",
        StepOfProduct.BTS_GH => "Băm gia công", StepOfProduct.CT_MD_HT_GH => "Trộn → Đùn → Hoàn thiện",
        StepOfProduct.MD_HT_GH => "Đùn → Hoàn thiện", StepOfProduct.CT_HT_GH => "Trộn → Hoàn thiện",
        StepOfProduct.CT_MDTS_GH => "Trộn → Đùn tái sinh", _ => string.Empty
    };
}
