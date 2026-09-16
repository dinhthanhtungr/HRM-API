using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
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
        var companyId = PurchaseOrderAccess.RequireCompanyId(_user); var query = _db.PurchaseOrders.AsNoTracking().Where(x => x.CompanyId == companyId && x.IsActive == true);
        if (request.SupplierId is { } supplierId && supplierId != Guid.Empty) query = query.Where(x => x.SupplierId == supplierId);
        if (!string.IsNullOrWhiteSpace(request.Status)) query = query.Where(x => x.Status == request.Status.Trim());
        if (!string.IsNullOrWhiteSpace(request.OrderType)) query = query.Where(x => x.OrderType == request.OrderType.Trim());
        if (request.From.HasValue) { var from = request.From.Value.Date; query = query.Where(x => x.CreateDate >= from); }
        if (request.To.HasValue) { var to = request.To.Value.Date.AddDays(1); query = query.Where(x => x.CreateDate < to); }
        if (request.NormalizedKeyword is { } keyword)
        {
            var pattern = PostgresSearchPattern.ContainsLiteral(keyword);
            query = query.Where(x =>
                EF.Functions.ILike(x.ExternalId ?? string.Empty, pattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Supplier!.SupplierName ?? string.Empty, pattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Supplier!.ExternalId ?? string.Empty, pattern, PostgresSearchPattern.EscapeCharacter) ||
                x.PurchaseOrderDetails.Any(d => d.IsActive &&
                    (EF.Functions.ILike(d.MaterialExternalIDSnapshot ?? string.Empty, pattern, PostgresSearchPattern.EscapeCharacter) ||
                     EF.Functions.ILike(d.MaterialNameSnapshot ?? string.Empty, pattern, PostgresSearchPattern.EscapeCharacter))) ||
                x.PurchaseOrderLinks.Any(link => link.IsActive && link.MerchandiseOrder != null &&
                    EF.Functions.ILike(link.MerchandiseOrder.ExternalId, pattern, PostgresSearchPattern.EscapeCharacter)));
        }
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreateDate).ThenByDescending(x => x.PurchaseOrderId)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize).Take(request.NormalizedPageSize)
            .Select(x => new PurchaseOrderListItemDto
            {
                PurchaseOrderId = x.PurchaseOrderId, ExternalId = x.ExternalId ?? string.Empty,
                Status = x.Status ?? string.Empty, SupplierId = x.SupplierId,
                SupplierName = x.Supplier == null ? string.Empty : x.Supplier.SupplierName ?? string.Empty,
                RequestDeliveryDate = x.RequestDeliveryDate, RealDeliveryDate = x.RealDeliveryDate,
                TotalPrice = x.PurchaseOrderSnapshot == null ? x.PurchaseOrderDetails.Where(d => d.IsActive).Sum(d => d.TotalPriceAgreed) ?? 0 : x.PurchaseOrderSnapshot.TotalPrice ?? 0,
                RealTotalPrice = 0,
                CreateDate = x.CreateDate,
                MerchandiseOrderCodes = string.Join(", ", x.PurchaseOrderLinks
                    .Where(link => link.IsActive && link.MerchandiseOrder != null)
                    .Select(link => link.MerchandiseOrder!.ExternalId))
            }).ToListAsync(ct);

        var codes = items.Select(item => item.ExternalId).Where(code => code.Length > 0).ToArray();
        var receipts = await _receiptReader.ReadAsync(companyId, codes, ct);
        var prices = await _db.PurchaseOrderDetails.AsNoTracking()
            .Where(detail => detail.IsActive && codes.Contains(detail.PurchaseOrder.ExternalId!))
            .Select(detail => new
            {
                Code = detail.PurchaseOrder.ExternalId!,
                ProductCode = detail.MaterialExternalIDSnapshot ?? string.Empty,
                UnitPrice = detail.UnitPriceAgreed ?? 0
            })
            .ToListAsync(ct);

        foreach (var item in items)
        {
            var orderReceipts = receipts.Where(receipt => receipt.PurchaseOrderCode == item.ExternalId).ToList();
            item.RealDeliveryDate = orderReceipts.Count == 0
                ? item.RealDeliveryDate
                : orderReceipts.Max(receipt => receipt.LastReceiptDate);
            item.RealTotalPrice = orderReceipts.Sum(receipt =>
                receipt.Quantity * prices
                    .Where(price => price.Code == item.ExternalId && price.ProductCode == receipt.ProductCode)
                    .Select(price => price.UnitPrice)
                    .FirstOrDefault());
        }
        return new PagedResult<PurchaseOrderListItemDto>(items, count, request.NormalizedPageNumber, request.NormalizedPageSize);
    }
}
