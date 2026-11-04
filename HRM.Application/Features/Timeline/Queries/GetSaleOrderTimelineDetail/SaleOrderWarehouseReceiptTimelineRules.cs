using HRM.Application.Features.Timeline.Dtos;

namespace HRM.Application.Features.Timeline.Queries.GetSaleOrderTimelineDetail;

internal static class SaleOrderWarehouseReceiptTimelineRules
{
    internal static bool IsDeliveryExportCode(string? codeFromRequest)
        => codeFromRequest?.Trim().StartsWith("PGH", StringComparison.OrdinalIgnoreCase) == true;

    internal static string? ResolveStatus(decimal importedQuantity, decimal requestedQuantity)
        => importedQuantity <= 0 ? null
            : importedQuantity < requestedQuantity ? SaleOrderWarehouseReceiptTimelineQuery.PartialStatus
            : SaleOrderWarehouseReceiptTimelineQuery.ImportedStatus;

    internal static IReadOnlyList<TimelineItemDto> BuildEvents(
        IEnumerable<TimelineItemDto> receipts, decimal requestedQuantity, string? filter)
    {
        var result = new List<TimelineItemDto>();
        decimal cumulative = 0;
        foreach (var receipt in receipts.OrderBy(x => x.CreatedDate))
        {
            cumulative += receipt.ReceiptQuantity ?? 0;
            var status = ResolveStatus(cumulative, requestedQuantity)
                ?? SaleOrderWarehouseReceiptTimelineQuery.PartialStatus;
            if (!string.IsNullOrWhiteSpace(filter) && filter.Trim() != status)
            {
                continue;
            }

            result.Add(new TimelineItemDto
            {
                SourceType = receipt.SourceType,
                SourceId = receipt.SourceId,
                SourceCode = receipt.SourceCode,
                EventType = receipt.EventType,
                Status = status,
                Note = receipt.Note,
                CreatedDate = receipt.CreatedDate,
                CreatedBy = receipt.CreatedBy,
                CreatedByName = receipt.CreatedByName,
                CompanyId = receipt.CompanyId,
                CompanyName = receipt.CompanyName,
                PayloadJson = receipt.PayloadJson,
                ReceiptQuantity = receipt.ReceiptQuantity,
                ImportedQuantity = cumulative
            });
        }

        return result;
    }
}
