using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HRM.Domain.Enums.WareHouses;
namespace HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrders;

internal sealed class GetPurchaseOrdersQueryHandler : IRequestHandler<GetPurchaseOrdersQuery, PagedResult<PurchaseOrderListItemDto>>
{
    private readonly IPurchaseOrderDbContext _db;
    private readonly ICurrentUser _user;
    private readonly PurchaseOrderReceiptReader _receiptReader;
    public GetPurchaseOrdersQueryHandler(IPurchaseOrderDbContext db, ICurrentUser user, PurchaseOrderReceiptReader receiptReader) =>
        (_db, _user, _receiptReader) = (db, user, receiptReader);
    public async Task<PagedResult<PurchaseOrderListItemDto>> Handle(GetPurchaseOrdersQuery request, CancellationToken ct)
    {
        var companyId = PurchaseOrderAccess.RequireCompanyId(_user); var query = _db.PurchaseOrders.AsNoTracking().Where(x => x.CompanyId == companyId && (request.IncludeInactive || x.IsActive == true));
        if (request.SupplierId is { } supplierId && supplierId != Guid.Empty) query = query.Where(x => x.SupplierId == supplierId);
        if (!string.IsNullOrWhiteSpace(request.Status)) query = query.Where(x => x.Status == request.Status.Trim());
        if (!string.IsNullOrWhiteSpace(request.OrderType)) query = query.Where(x => x.OrderType == request.OrderType.Trim());
        if (request.From.HasValue) { var from = request.From.Value.Date; query = query.Where(x => x.CreateDate >= from); }
        if (request.To.HasValue) { var to = request.To.Value.Date.AddDays(1); query = query.Where(x => x.CreateDate < to); }
        var smartSearch = HRM.Application.Commons.Searching.SmartSearch.For(query, fields => [
            fields.Code("purchaseOrderCode", x => x.ExternalId, "Mã đơn mua", "Purchase order code"),
            fields.Code("supplierCode", x => x.Supplier != null && x.Supplier.CompanyId == companyId ? x.Supplier.ExternalId : "", "Mã NCC", "Supplier code"),
            fields.Name("supplierName", x => x.Supplier != null && x.Supplier.CompanyId == companyId ? x.Supplier.SupplierName : "", "Tên NCC", "Supplier name"),
            fields.Codes("materialCode", x => x.PurchaseOrderDetails.Where(d => request.IncludeInactive || d.IsActive),
                d => d.MaterialExternalIDSnapshot, "Mã NVL", "Material code"),
            fields.Names("materialName", x => x.PurchaseOrderDetails.Where(d => request.IncludeInactive || d.IsActive),
                d => d.MaterialNameSnapshot, "Tên NVL", "Material name")
        ]);
        query = smartSearch.ApplyRequest(query, request.Keyword, request, out var smartKeyword);
        query = PurchaseOrderKeywordSearch.Apply(query, smartKeyword, companyId, request.IncludeInactive);
        if (await smartSearch.SuggestionsAsync<PurchaseOrderListItemDto>(query, request, ct) is { } suggestions)
            return suggestions;
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreateDate).ThenByDescending(x => x.PurchaseOrderId)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize).Take(request.NormalizedPageSize)
            .Select(x => new PurchaseOrderListItemDto
            {
                PurchaseOrderId = x.PurchaseOrderId,
                ExternalId = x.ExternalId ?? string.Empty,
                Status = x.Status ?? string.Empty,
                SupplierId = x.SupplierId,
                SupplierName = x.Supplier == null ? string.Empty : x.Supplier.SupplierName ?? string.Empty,
                OrderType = x.OrderType,
                Comment = x.Comment,
                IsActive = x.IsActive == true,
                SupplierExternalId = x.Supplier == null ? null : x.Supplier.ExternalId,
                Details = x.PurchaseOrderDetails.Where(d => request.IncludeInactive || d.IsActive).OrderBy(d => d.LineNo).Select(d => new PurchaseOrderLineDto
                {
                    PurchaseOrderDetailId = d.PurchaseOrderDetailId,
                    LineNo = d.LineNo,
                    MaterialId = d.MaterialId,
                    MaterialCode = d.MaterialExternalIDSnapshot ?? "",
                    MaterialName = d.MaterialNameSnapshot ?? "",
                    Quantity = d.RequestQuantity ?? 0m,
                    RealQuantity = d.RealQuantity,
                    UnitPriceAgreed = d.UnitPriceAgreed ?? 0m,
                    TotalPriceAgreed = d.TotalPriceAgreed ?? 0m,
                    Package = d.Package,
                    DeliveryDate = d.DeliveryDate,
                    Note = d.Note
                }).ToList(),
                RequestDeliveryDate = x.RequestDeliveryDate,
                RealDeliveryDate = null,
                Subtotal = x.PurchaseOrderDetails.Where(d => request.IncludeInactive || d.IsActive).Sum(d => d.TotalPriceAgreed) ?? 0,
                Vat = x.PurchaseOrderSnapshot == null ? null : x.PurchaseOrderSnapshot.Vat,
                StoredTotalPrice = x.PurchaseOrderSnapshot == null ? null : x.PurchaseOrderSnapshot.TotalPrice,
                RealTotalPrice = x.PurchaseOrderDetails.Where(d => d.IsActive).Sum(d => (d.RealQuantity ?? 0m) * (d.UnitPriceAgreed ?? (d.RequestQuantity.HasValue && d.RequestQuantity != 0 && d.TotalPriceAgreed.HasValue ? d.TotalPriceAgreed.Value / d.RequestQuantity.Value : 0m))),
                CreateDate = x.CreateDate,
                MerchandiseOrderCodes = string.Join(", ", x.PurchaseOrderLinks
                    .Where(link => (request.IncludeInactive || link.IsActive) && link.MerchandiseOrder != null &&
                        link.MerchandiseOrder.CompanyId == companyId)
                    .Select(link => link.MerchandiseOrder!.ExternalId))
            }).ToListAsync(ct);

        var codes = items.Select(item => item.ExternalId.Trim()).Where(code => code.Length > 0).ToArray();
        var iqcDates = await _receiptReader.ReadIqcDatesAsync(companyId, codes, ct);
        var normalizedCodes = codes.Select(PurchaseReturnReadModel.Normalize).ToArray();
        var returnCodes = await _db.WarehouseRequests.AsNoTracking().Where(x => x.CompanyId == companyId &&
            x.ReqType == WareHouseRequestType.ExportReturn && x.codeFromRequest != null &&
            normalizedCodes.Contains(x.codeFromRequest.Trim().ToUpper()))
            .Select(x => x.codeFromRequest.Trim().ToUpper()).Distinct().ToListAsync(ct);
        var returns = await new PurchaseReturnReadModel(_db).ReadAsync(companyId, returnCodes, ct);
        foreach (var item in items)
        {
            item.RealDeliveryDate = iqcDates.TryGetValue(item.ExternalId.Trim(), out var iqcDate) ? iqcDate : null;
            item.ReturnProgress = returns.TryGetValue(PurchaseReturnReadModel.Normalize(item.ExternalId), out var returned) ? returned.Progress : null;

        }
        return new PagedResult<PurchaseOrderListItemDto>(items, count, request.NormalizedPageNumber, request.NormalizedPageSize);
    }
}
