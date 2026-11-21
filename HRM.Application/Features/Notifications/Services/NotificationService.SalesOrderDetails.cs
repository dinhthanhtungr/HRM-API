using System.Text.Json;
using HRM.Application.Features.Notifications.Dtos;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Notifications.Services;

internal sealed partial class NotificationService
{
    // Input is a page already filtered by company and the current employee's inbox.
    // Never accept order IDs from request parameters or expand the notification audience.
    private async Task ApplySalesOrderDetailsAsync(List<NotificationDto> items, CancellationToken ct)
    {
        if (_saleOrderDb is null || items.Count == 0) return;
        var references = items.ToDictionary(n => n.Id, SalesOrderReferences);
        var ids = references.Values.SelectMany(x => x.Ids).Distinct().ToArray();
        var codes = references.Values.SelectMany(x => x.Codes).Distinct().ToArray();
        if (ids.Length == 0 && codes.Length == 0) return;
        var company = GetCurrentCompanyId();
        var orders = await _saleOrderDb.MerchandiseOrders.AsNoTracking()
            .Where(o => o.CompanyId == company && o.IsActive &&
                (ids.Contains(o.MerchandiseOrderId) || codes.Contains(o.ExternalId)))
            .Select(o => new { o.MerchandiseOrderId, o.ExternalId, o.CustomerExternalIdSnapshot, o.CustomerNameSnapshot })
            .ToListAsync(ct);
        // A legacy code is usable only when it identifies one order in this company.
        orders = orders.Where(o => ids.Contains(o.MerchandiseOrderId) ||
            orders.Count(other => other.ExternalId == o.ExternalId) == 1).ToList();
        if (orders.Count == 0) return;
        var orderIds = orders.Select(o => o.MerchandiseOrderId).ToArray();
        var products = await _saleOrderDb.MerchandiseOrderDetails.AsNoTracking()
            .Where(d => d.IsActive && d.MerchandiseOrder.IsActive && d.MerchandiseOrder.CompanyId == company &&
                orderIds.Contains(d.MerchandiseOrderId))
            .Select(d => new { d.MerchandiseOrderId, d.ProductExternalIdSnapshot }).Distinct().ToListAsync(ct);
        foreach (var item in items)
        {
            var reference = references[item.Id];
            item.SalesOrderInfo = orders.Where(o => reference.Ids.Contains(o.MerchandiseOrderId) || reference.Codes.Contains(o.ExternalId))
                .OrderBy(o => o.ExternalId, StringComparer.Ordinal).ThenBy(o => o.MerchandiseOrderId)
                .Select(o => new NotificationSalesOrderInfoDto(o.MerchandiseOrderId, o.ExternalId,
                    o.CustomerExternalIdSnapshot, o.CustomerNameSnapshot,
                    products.Where(d => d.MerchandiseOrderId == o.MerchandiseOrderId)
                        .Select(d => d.ProductExternalIdSnapshot?.Trim()).Where(c => !string.IsNullOrWhiteSpace(c))
                        .Select(c => c!).Distinct(StringComparer.Ordinal).OrderBy(c => c, StringComparer.Ordinal).ToArray()))
                .ToArray();
        }
    }

    internal static (HashSet<Guid> Ids, HashSet<string> Codes) SalesOrderReferences(NotificationDto item)
    {
        var ids = new HashSet<Guid>();
        var codes = new HashSet<string>(StringComparer.Ordinal);
        if (item.Context?.AggregateType == "MerchandiseOrder")
        {
            if (item.Context.AggregateId is { } id && id != Guid.Empty) ids.Add(id);
            if (ids.Count == 0 && !string.IsNullOrWhiteSpace(item.Context.AggregateCode)) codes.Add(item.Context.AggregateCode);
        }
        try
        {
            using var doc = JsonDocument.Parse(item.PayloadJson ?? "{}");
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("merchandiseOrderId", out var rootId) && rootId.ValueKind == JsonValueKind.String &&
                Guid.TryParse(rootId.GetString(), out var parsedId) && parsedId != Guid.Empty) ids.Add(parsedId);
            if (ids.Count > 0) codes.Clear();
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("merchandiseOrders", out var rows) && rows.ValueKind == JsonValueKind.Array)
                foreach (var row in rows.EnumerateArray())
                {
                    if (row.ValueKind != JsonValueKind.Object) continue;
                    var hasId = false;
                    if (row.TryGetProperty("id", out var value) && value.ValueKind == JsonValueKind.String &&
                        Guid.TryParse(value.GetString(), out var id) && id != Guid.Empty) { ids.Add(id); hasId = true; }
                    // Code-only references support older payloads; no guessing from message/title.
                    if (!hasId && row.TryGetProperty("externalId", out var code) && code.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(code.GetString())) codes.Add(code.GetString()!);
                }
        }
        catch (JsonException) { /* Historical payloads may be malformed. */ }
        return (ids, codes);
    }
}
