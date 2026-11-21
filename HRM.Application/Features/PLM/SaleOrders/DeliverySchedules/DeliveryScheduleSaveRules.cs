namespace HRM.Application.Features.PLM.SaleOrders.DeliverySchedules;

public static class DeliveryScheduleSaveRules
{
    public static string? Validate(decimal ordered, IReadOnlyList<DeliveryScheduleRowDto> current,
        SaveDeliverySchedulesRequest request, bool canEditRequest, bool canEditPromise)
    {
        if (request.Items.Count is < 1 or > 100 || request.Items.Any(x => x is null || x.Quantity <= 0 || x.Quantity >= 1_000_000_000_000_000m || decimal.Round(x.Quantity, 3) != x.Quantity
            || x.RequestedDate == default || x.PromisedDate == default(DateOnly) || (x.RequestNote?.Length ?? 0) > 2000 || (x.PromiseNote?.Length ?? 0) > 2000))
            return "Lịch cần 1–100 đợt, lượng dương tối đa 3 số lẻ, ngày hợp lệ và ghi chú tối đa 2000 ký tự.";
        if (request.Items.Sum(x => x.Quantity) != ordered) return "Tổng lượng các đợt phải bằng lượng đặt.";
        var ids = request.Items.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToArray();
        var existing = current.Select(x => x.Id).ToHashSet();
        if (ids.Distinct().Count() != ids.Length || request.RemoveIds.Distinct().Count() != request.RemoveIds.Count
            || ids.Any(id => !existing.Contains(id)) || request.RemoveIds.Any(id => !existing.Contains(id))
            || ids.Intersect(request.RemoveIds).Any() || existing.Except(ids.Concat(request.RemoveIds)).Any())
            return "ID đợt trùng, ngoài phạm vi hoặc thiếu. Hãy tải lại lịch.";
        if (!canEditRequest && (request.Items.Any(x => !x.Id.HasValue) || request.RemoveIds.Count > 0))
            return "Không có quyền thêm hoặc xóa đợt.";
        foreach (var row in current)
        {
            if (request.RemoveIds.Contains(row.Id))
            {
                if (row.AllocatedQuantity > 0) return "Không được xóa đợt đã phân bổ vào phiếu giao.";
                continue;
            }
            var input = request.Items.Single(x => x.Id == row.Id);
            if (input.Quantity < row.AllocatedQuantity) return "Lượng đợt không được nhỏ hơn lượng đã phân bổ.";
            if (!canEditRequest && (input.Quantity != row.Quantity || input.RequestedDate != row.RequestedDate || input.RequestNote != row.RequestNote))
                return "Không có quyền đổi lượng/ngày yêu cầu của đợt.";
            if (!canEditPromise && (input.PromisedDate != row.PromisedDate || input.PromiseNote != row.PromiseNote))
                return "Không có quyền đổi ngày hứa giao.";
        }
        if (!canEditPromise && request.Items.Any(x => !x.Id.HasValue && (x.PromisedDate.HasValue || !string.IsNullOrEmpty(x.PromiseNote))))
            return "Không có quyền chốt ngày hứa giao cho đợt mới.";
        return null;
    }
}
