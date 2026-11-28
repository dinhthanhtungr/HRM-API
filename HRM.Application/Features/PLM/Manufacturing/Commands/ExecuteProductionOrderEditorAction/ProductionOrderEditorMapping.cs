using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders;
using HRM.Domain.Entities.ManufacturingSchema;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction;

internal static class ProductionOrderEditorMapping
{
    public static string? Validate(ProductionOrderEditorRequest r)
    {
        if (r.Action == "RollbackUseStock")
            return r.ClearFields is { Count: 0 } && r.TotalQuantity is null && r.NumOfBatches is null
                && r.StepOfProduct is null && r.ExpectedDate is null && r.ManufacturingDate is null
                && r.BagType is null && r.LabNote is null && r.PlpuNote is null && r.Requirement is null
                && r.QcCheck is null && r.FormulaNote is null && r.SelectedFormulaId is null
                && r.SourceFormulaId is null && r.Origin is null && r.Items is null
                    ? null : "Quay về Mới chỉ nhận action và revision; hãy lưu hoặc hủy bản nháp trước.";
        if (r.ClearFields is null || r.ClearFields.Count > ManufacturingEditorPolicy.HeaderFields.Length
            || r.ClearFields.Distinct(StringComparer.Ordinal).Count() != r.ClearFields.Count)
            return "Danh sách field cần xóa không hợp lệ.";
        foreach (var field in r.ClearFields)
        {
            object? value = field switch
            {
                "stepOfProduct" => r.StepOfProduct, "expectedDate" => r.ExpectedDate, "manufacturingDate" => r.ManufacturingDate,
                "labNote" => r.LabNote, "plpuNote" => r.PlpuNote, "requirement" => r.Requirement, "qcCheck" => r.QcCheck,
                "formulaNote" => r.FormulaNote, "bagType" => r.BagType, _ => false
            };
            if (value is not null) return "Field cần xóa không được hỗ trợ hoặc đồng thời có giá trị.";
        }
        if (r.TotalQuantity is <= 0 || r.NumOfBatches is <= 0
            || r.StepOfProduct.HasValue && !Enum.IsDefined(r.StepOfProduct.Value)
            || r.Items is { Count: 0 } || r.Items?.Count > 1000)
            return "Khối lượng, số mẻ, công đoạn hoặc thành phần công thức không hợp lệ.";
        if (r.Action != "Save" && (r.ClearFields.Count != 0 || r.BagType is not null || r.SelectedFormulaId.HasValue))
            return "Chỉ thao tác Lưu hỗ trợ xóa field, đổi bao bì hoặc sửa VA đang chọn.";
        if (r.Action is not ("Save" or "SaveAndCreateFormula") && r.Items is not null)
            return "Thao tác không hỗ trợ sửa vật tư.";
        if (r.Action is not ("SaveAndCreateFormula" or "UseStock" or "RequestFormula") && r.SourceFormulaId.HasValue
            || r.Action != "SaveAndCreateFormula" && r.Origin.HasValue
            || r.Action is "RequestFormula" or "Schedule" or "UseStock" && r.ManufacturingDate.HasValue)
            return "Thao tác không hỗ trợ đổi nguồn hoặc ngày sản xuất.";
        if (r.FormulaNote is not null || r.ClearFields.Contains("formulaNote"))
        {
            if (r.Action != "Save" || !r.SelectedFormulaId.HasValue || r.SelectedFormulaId == Guid.Empty
                || r.FormulaNote?.Length > 20000)
                return "Ghi chú VA chỉ được lưu cho công thức đang chọn.";
        }
        return null;
    }

    public static ProductionOrderInformRequest Merge(MfgProductionOrder o, ProductionOrderEditorRequest r)
    {
        bool Clear(string key) => r.ClearFields.Contains(key, StringComparer.Ordinal);
        return new()
        {
            TotalQuantity = r.TotalQuantity ?? o.TotalQuantity ?? 0,
            NumOfBatches = r.NumOfBatches ?? o.NumOfBatches ?? 0,
            StepOfProduct = Clear("stepOfProduct") ? null : r.StepOfProduct ?? o.StepOfProduct,
            ExpectedDate = Clear("expectedDate") ? null : r.ExpectedDate ?? o.ExpectedDate,
            ManufacturingDate = Clear("manufacturingDate") ? null : r.ManufacturingDate ?? o.ManufacturingDate,
            BagType = Clear("bagType") ? "" : r.BagType ?? o.BagType,
            LabNote = Clear("labNote") ? "" : r.LabNote ?? o.LabNote,
            PlpuNote = Clear("plpuNote") ? "" : r.PlpuNote ?? o.PlpuNote,
            Requirement = Clear("requirement") ? "" : r.Requirement ?? o.Requirement,
            QcCheck = Clear("qcCheck") ? "" : r.QcCheck ?? o.QcCheck,
            SelectedFormulaId = r.SelectedFormulaId, Items = r.Items
        };
    }
}
