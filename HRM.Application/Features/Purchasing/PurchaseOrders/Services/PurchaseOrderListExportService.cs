using HRM.Application.Commons.Authorization;
using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;
namespace HRM.Application.Features.Purchasing.PurchaseOrders.Services;

using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrders;
internal sealed class PurchaseOrderListExportService(IPurchaseOrderDbContext db, ICurrentUser user, IPurchaseOrderExcelRenderer renderer, ICurrentUserPermissionService permissions)
{
    public async Task<byte[]> ExportAsync(GetPurchaseOrdersQuery query, CancellationToken ct)
    {
        if (!user.IsAuthenticated || !PurchaseOrderAccess.CanManage(permissions)) throw new UnauthorizedAccessException("Không có quyền xuất danh sách PO.");
        var companyId = PurchaseOrderAccess.RequireCompanyId(user);

        var poQ = db.PurchaseOrders.AsNoTracking().Where(po => po.CompanyId == companyId
            && (query.IncludeInactive || po.IsActive == true)
            && po.OrderType == PurchaseOrderExportRules.ReportOrderType
            && (string.IsNullOrEmpty(po.ExternalId) || !po.ExternalId.ToUpper().StartsWith("PO")));

        if (query.SupplierId.HasValue)
            poQ = poQ.Where(po => po.SupplierId == query.SupplierId.Value);

        if (!string.IsNullOrWhiteSpace(query.Status))
            poQ = poQ.Where(po => po.Status == query.Status);

        if (!string.IsNullOrWhiteSpace(query.OrderType))
            poQ = poQ.Where(po => po.OrderType == query.OrderType);

        if (query.From.HasValue)
        {
            var fromDate = query.From.Value.Date;
            poQ = poQ.Where(po => po.CreateDate >= fromDate);
        }

        if (query.To.HasValue)
        {
            var toExclusive = query.To.Value.Date.AddDays(1);
            poQ = poQ.Where(po => po.CreateDate < toExclusive);
        }

        poQ = PurchaseOrderKeywordSearch.Apply(poQ, query.Keyword, companyId, query.IncludeInactive);

        var orderLines = await poQ
            .SelectMany(po => po.PurchaseOrderDetails
                .Where(d => (query.IncludeInactive || d.IsActive)
                    && (d.MaterialExternalIDSnapshot ?? "").ToUpper().StartsWith("NVL")
                    && !(d.MaterialExternalIDSnapshot ?? "").ToUpper().Contains("VPP")
                    && !(d.MaterialExternalIDSnapshot ?? "").ToUpper().Contains("BB"))
                .Select(d => new PurchaseOrderExportLine
                {
                    PurchaseOrderId = po.PurchaseOrderId,
                    POExternalId = po.ExternalId ?? string.Empty,
                    Status = po.Status ?? string.Empty,
                    OrderType = po.OrderType ?? string.Empty,
                    CreatedDate = po.CreateDate,
                    HeaderRequestDeliveryDate = po.RequestDeliveryDate,
                    SupplierExternalId = po.Supplier != null ? po.Supplier!.ExternalId : null,
                    SupplierName = po.Supplier != null ? po.Supplier!.SupplierName : null,
                    PurchaseOrderDetailId = d.PurchaseOrderDetailId,
                    LineNo = d.LineNo,
                    MaterialCode = d.MaterialExternalIDSnapshot ?? string.Empty,
                    MaterialName = d.MaterialNameSnapshot ?? string.Empty,
                    Unit = d.Package ?? string.Empty,
                    OrderedQuantity = d.RequestQuantity ?? 0m,
                    UnitPriceAgreed = d.UnitPriceAgreed,
                    TotalPriceAgreed = d.TotalPriceAgreed,
                    BaseCostSnapshot = d.BaseCostSnapshot,
                    DeliveryDate = d.DeliveryDate
                }))
            .OrderBy(x => x.CreatedDate)
            .ThenBy(x => x.POExternalId)
            .ThenBy(x => x.LineNo)
            .ToListAsync(ct);

        var poExternalIds = orderLines
            .Where(x => !string.IsNullOrWhiteSpace(x.POExternalId))
            .Select(x => x.POExternalId.Trim())
            .Distinct()
            .ToList();

        var receiptRows = poExternalIds.Count == 0
            ? new List<PurchaseOrderReceiptExportRow>()
            : await new PurchaseOrderExportReceiptReader(db).BuildPurchaseOrderReceiptRowsAsync(poExternalIds, companyId, ct);

        var allocatedReceipts = PurchaseOrderExportRules.AllocatePurchaseOrderReceipts(orderLines, receiptRows);
        var receiptsByDetailId = allocatedReceipts
            .Where(x => x.PurchaseOrderDetailId.HasValue)
            .GroupBy(x => x.PurchaseOrderDetailId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());

        var rows = orderLines.Select(line =>
        {
            receiptsByDetailId.TryGetValue(line.PurchaseOrderDetailId, out var receipts);
            receipts ??= new List<PurchaseOrderReceiptExportRow>();

            var receivedQuantity = receipts.Sum(x => x.ReceivedQuantity);
            var acceptedQuantity = receipts.Sum(x => x.AcceptedQuantity);
            var pendingQcQuantity = receipts.Sum(x => x.PendingQcQuantity);
            var rejectedQuantity = receipts.Sum(x => x.RejectedQuantity);
            var actualDeliveryDate = receipts.Count > 0
                ? receipts.Max(x => x.ReceiptDate)
                : (DateTime?)null;
            var requestDeliveryDate = line.DeliveryDate ?? line.HeaderRequestDeliveryDate;
            var lateDays = PurchaseOrderExportRules.CalculateLateDays(requestDeliveryDate, actualDeliveryDate);
            var unitPrice = PurchaseOrderExportRules.ResolvePurchaseOrderUnitPrice(
                line.OrderedQuantity,
                line.UnitPriceAgreed,
                line.TotalPriceAgreed,
                line.BaseCostSnapshot);

            return new PurchaseOrderExcelRow
            {
                OrderDate = line.CreatedDate,
                PurchaseOrderCode = line.POExternalId,
                Status = line.Status,
                OrderType = line.OrderType,
                ActualDeliveryDate = actualDeliveryDate,
                RequestDeliveryDate = requestDeliveryDate,
                LateDays = lateDays,
                DeliveryResult = PurchaseOrderExportRules.ResolveDeliveryResult(actualDeliveryDate, lateDays),
                SupplierExternalId = line.SupplierExternalId,
                SupplierName = line.SupplierName,
                MaterialCode = line.MaterialCode,
                MaterialName = line.MaterialName,
                Unit = line.Unit,
                OrderedQuantity = line.OrderedQuantity,
                ReceivedQuantity = receivedQuantity,
                AcceptedQuantity = acceptedQuantity,
                PendingQcQuantity = pendingQcQuantity,
                RejectedQuantity = rejectedQuantity,
                UnitPrice = unitPrice,

                RealTotalAmount = Math.Round(
                    PurchaseOrderExportRules.ResolveRealTotalAmount(
                        receivedQuantity,
                        unitPrice),
                    2,
                    MidpointRounding.AwayFromZero)
            };
        }).ToList();

        return renderer.RenderList(rows);
    }
}
