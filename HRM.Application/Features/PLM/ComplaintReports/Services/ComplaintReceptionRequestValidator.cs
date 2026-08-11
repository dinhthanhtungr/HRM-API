using HRM.Application.Features.PLM.ComplaintReports.Dtos;

namespace HRM.Application.Features.PLM.ComplaintReports.Services;

internal static class ComplaintReceptionRequestValidator
{
    internal const int MaxLineCount = 50;
    internal const int MaxLotsPerLine = 100;

    internal static string? Validate(
        string? summary,
        string? nonConformityDescription,
        Domain.Enums.Orders.ComplaintResolutionType requestedResolutionType,
        IReadOnlyCollection<ComplaintReportLineRequest>? lines)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return "Tóm tắt khiếu nại là bắt buộc.";
        }

        if (summary.Trim().Length > 4_000)
        {
            return "Tóm tắt khiếu nại không được vượt quá 4.000 ký tự.";
        }

        if (nonConformityDescription?.Trim().Length > 20_000)
        {
            return "Mô tả sự không phù hợp không được vượt quá 20.000 ký tự.";
        }

        if (!Enum.IsDefined(requestedResolutionType))
        {
            return "Đề nghị hướng xử lý không hợp lệ.";
        }

        if (lines is null || lines.Count == 0 || lines.Count > MaxLineCount)
        {
            return $"Khiếu nại phải có từ 1 đến {MaxLineCount} dòng đơn nguồn.";
        }

        if (lines.Any(x => x.SourceMerchandiseOrderDetailId == Guid.Empty))
        {
            return "Dòng đơn nguồn không hợp lệ.";
        }

        if (lines.Select(x => x.SourceMerchandiseOrderDetailId).Distinct().Count() != lines.Count)
        {
            return "Không được chọn trùng dòng đơn nguồn.";
        }

        foreach (var line in lines)
        {
            if (line.ComplaintQuantity <= 0)
            {
                return "Số lượng khiếu nại của mỗi dòng phải lớn hơn 0.";
            }

            if (line.IssueType?.Trim().Length > 100 || line.Severity?.Trim().Length > 50)
            {
                return "Loại vấn đề hoặc mức độ vượt quá độ dài cho phép.";
            }

            if (line.Description?.Trim().Length > 20_000)
            {
                return "Mô tả dòng khiếu nại không được vượt quá 20.000 ký tự.";
            }

            if (line.Lots.Count == 0 || line.Lots.Count > MaxLotsPerLine)
            {
                return $"Mỗi dòng khiếu nại phải có từ 1 đến {MaxLotsPerLine} lot đã giao.";
            }

            if (line.Lots.Any(x => x.SourceDeliveryOrderDetailId == Guid.Empty || x.ComplaintQuantity <= 0))
            {
                return "Delivery detail và số lượng khiếu nại theo lot phải hợp lệ.";
            }

            var duplicateLots = line.Lots
                .GroupBy(x => new { x.SourceDeliveryOrderDetailId, x.SourceLotConsumptionId })
                .Any(group => group.Count() > 1);
            if (duplicateLots)
            {
                return "Không được chọn trùng lot trong cùng một dòng khiếu nại.";
            }

            if (line.Lots.Sum(x => x.ComplaintQuantity) != line.ComplaintQuantity)
            {
                return "Tổng số lượng các lot phải bằng số lượng khiếu nại của dòng.";
            }
        }

        return null;
    }
}
