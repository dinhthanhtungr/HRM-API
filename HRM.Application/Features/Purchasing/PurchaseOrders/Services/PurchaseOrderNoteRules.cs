using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Domain.Entities.OrderSchema;
using HRM.Application.Commons.Patching;
namespace HRM.Application.Features.Purchasing.PurchaseOrders.Services;

internal static class PurchaseOrderNoteRules
{
    public static UpdatePurchaseOrderNotesRequest IgnoreClientDeliveryDate(UpdatePurchaseOrderNotesRequest request) => request with
    {
        RealDeliveryDate = null,
        ClearFields = request.ClearFields?.Where(x => !string.Equals(x, "realDeliveryDate", StringComparison.OrdinalIgnoreCase)).ToArray()!
    };

    private static readonly HashSet<string> AllowedFields = new(StringComparer.OrdinalIgnoreCase)
        { "comment", "plpuComment", "realDeliveryDate", "paymentTypes", "vat", "deliveryAddress" };
    public static bool IsInternalNoteOnly(UpdatePurchaseOrderNotesRequest request) =>
        (request.PlpuComment is not null || request.ClearFields.Contains("plpuComment", StringComparer.OrdinalIgnoreCase)) &&
            request.Comment is null && request.OrderType is null && !request.RequestDeliveryDate.HasValue &&
            !request.RealDeliveryDate.HasValue && request.PaymentTypes is null && !request.Vat.HasValue &&
            request.DeliveryAddress is null && !request.IsActive.HasValue &&
            request.ClearFields.All(x => string.Equals(x, "plpuComment", StringComparison.OrdinalIgnoreCase));
    public static string? Validate(UpdatePurchaseOrderNotesRequest request)
    {
        if (request.ClearFields is null) return "ClearFields không được null.";
        var clear = request.ClearFields.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (clear.Any(field => !AllowedFields.Contains(field))) return "ClearFields chỉ hỗ trợ comment, plpuComment, realDeliveryDate, paymentTypes, vat, deliveryAddress.";
        if (request.OrderType is not null && !PurchaseOrderEditRules.ValidOrderType(request.OrderType)) return "Loại đơn phải là NCC, KH hoặc NB.";
        if (request.Vat is < 0 or > 100) return "VAT phải nằm trong khoảng 0-100.";
        if (request.IsActive == false && (request.OrderType is not null || request.RequestDeliveryDate.HasValue ||
            request.RealDeliveryDate.HasValue || request.PaymentTypes is not null || request.DeliveryAddress is not null ||
            request.Vat.HasValue || clear.Any(x => !string.Equals(x, "comment", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(x, "plpuComment", StringComparison.OrdinalIgnoreCase))))
            return "Không kết hợp xóa đơn với cập nhật thông tin; dùng DELETE để xóa đơn.";
        if (clear.Contains("realDeliveryDate") && request.RealDeliveryDate.HasValue ||
            clear.Contains("paymentTypes") && request.PaymentTypes is not null ||
            clear.Contains("vat") && request.Vat.HasValue || clear.Contains("deliveryAddress") && request.DeliveryAddress is not null)
            return "Một field không thể vừa có giá trị vừa nằm trong ClearFields.";
        if (clear.Contains("comment") && request.Comment is not null || clear.Contains("plpuComment") && request.PlpuComment is not null)
            return "Một field không thể vừa có giá trị vừa nằm trong ClearFields.";
        if (request.Comment is null && request.PlpuComment is null && !request.IsActive.HasValue && clear.Count == 0 &&
            request.OrderType is null && !request.RequestDeliveryDate.HasValue && !request.RealDeliveryDate.HasValue &&
            request.PaymentTypes is null && !request.Vat.HasValue && request.DeliveryAddress is null)
            return "Không có nội dung cần cập nhật.";
        return null;
    }
    public static bool ApplyInformation(PurchaseOrder po, PurchaseOrderSnapshot snapshot, UpdatePurchaseOrderNotesRequest request)
    {
        var clear = request.ClearFields.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var changed = false;
        changed |= PatchHelper.SetIfNotNull(request.OrderType?.Trim(), () => po.OrderType, x => po.OrderType = x);
        if (request.RequestDeliveryDate.HasValue) changed |= PatchHelper.SetNullable(request.RequestDeliveryDate, () => po.RequestDeliveryDate, x => po.RequestDeliveryDate = x);
        if (clear.Contains("vat") || request.Vat.HasValue)
            changed |= PatchHelper.SetNullable(clear.Contains("vat") ? null : request.Vat, () => snapshot.Vat, x => snapshot.Vat = x);
        changed |= SetText("paymentTypes", request.PaymentTypes, () => snapshot.PaymentTypes, x => snapshot.PaymentTypes = x);
        changed |= SetText("deliveryAddress", request.DeliveryAddress, () => snapshot.DeliveryAddress, x => snapshot.DeliveryAddress = x);
        changed |= SetText("comment", request.Comment, () => po.Comment, x => po.Comment = x);
        changed |= SetText("plpuComment", request.PlpuComment, () => po.PLPUComment, x => po.PLPUComment = x);
        return changed;

        bool SetText(string field, string? value, Func<string?> current, Action<string?> apply) =>
            clear.Contains(field) ? PatchHelper.SetNullableRef(null, current, apply) : PatchHelper.SetIfNotNull(value?.Trim(), current, apply);
    }
    public static void Apply(PurchaseOrder po, UpdatePurchaseOrderNotesRequest request)
    {
        var clear = request.ClearFields.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (clear.Contains("comment")) po.Comment = null;
        else if (request.Comment is not null) po.Comment = request.Comment.Trim();
        if (clear.Contains("plpuComment")) po.PLPUComment = null;
        else if (request.PlpuComment is not null) po.PLPUComment = request.PlpuComment.Trim();
    }
}
