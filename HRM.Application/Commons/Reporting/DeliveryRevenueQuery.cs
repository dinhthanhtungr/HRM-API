using HRM.Application.Commons.Rules;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Enums.Deliveries;

namespace HRM.Application.Commons.Reporting;

/// <summary>
/// Dòng doanh số chuẩn được ghi nhận từ số lượng giao và đơn giá bán đã thỏa thuận.
/// </summary>
internal sealed class DeliveryRevenueLine
{
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public DateTime RevenueDate { get; init; }
    public string ProductKey { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string? ProductTypeKey { get; init; }
    public string? ProductTypeName { get; init; }
    public string? LotNoList { get; init; }
    public bool HasNormalizedLots { get; init; }
    public decimal LotCostSnapshotAmount { get; init; }
    public decimal Quantity { get; init; }
    public decimal RevenueAmountVnd { get; init; }
    public decimal BaseCostAmount { get; init; }
}

/// <summary>
/// Nguồn doanh số chuẩn của Application theo quy tắc P&L dựa trên DeliveryOrderDetail.
/// </summary>
internal static class DeliveryRevenueQuery
{
    public const string InternalCustomerExternalId = InternalCustomerRules.InternalCustomerExternalId;

    public static IQueryable<DeliveryRevenueLine> Create(
        IQueryable<DeliveryOrderDetail> source,
        DateTime from,
        DateTime toExclusive,
        Guid? companyId = null)
    {
        var reportableLines = source.Where(x => x.IsActive
            && x.DeliveryOrder.IsActive
            && x.DeliveryOrder.Status != DeliveryOrderStatus.Canceled.ToString()
            && x.DeliveryOrder.CustomerExternalIdSnapShot != InternalCustomerExternalId
            && x.DeliveryOrder.CreatedDate.HasValue
            && x.DeliveryOrder.CreatedDate.Value >= from
            && x.DeliveryOrder.CreatedDate.Value < toExclusive
            && x.MerchandiseOrderDetailId.HasValue);

        if (companyId.HasValue)
        {
            reportableLines = reportableLines
                .Where(x => x.DeliveryOrder.CompanyId == companyId.Value);
        }

        return reportableLines.Select(x => new DeliveryRevenueLine
        {
            CustomerId = x.MerchandiseOrderDetail!.MerchandiseOrder.CustomerId,
            CustomerName = x.MerchandiseOrderDetail.MerchandiseOrder.CustomerNameSnapshot,
            RevenueDate = x.DeliveryOrder.CreatedDate!.Value,
            ProductKey = x.Product != null
                ? x.Product.ColourCode ?? x.ProductExternalIdSnapShot ?? string.Empty
                : x.ProductExternalIdSnapShot ?? string.Empty,
            ProductName = x.Product != null
                ? x.Product.Name ?? x.ProductNameSnapShot ?? string.Empty
                : x.ProductNameSnapShot ?? string.Empty,
            ProductTypeKey = x.Product != null && x.Product.Category != null
                ? x.Product.Category.ExternalId
                : null,
            ProductTypeName = x.Product != null && x.Product.Category != null
                ? x.Product.Category.Name
                : null,
            LotNoList = x.LotConsumptions.Any(lot => lot.IsActive)
                ? string.Join(", ", x.LotConsumptions
                    .Where(lot => lot.IsActive)
                    .OrderBy(lot => lot.LotNo)
                    .Select(lot => lot.LotNo))
                : x.LotNoList,
            HasNormalizedLots = x.LotConsumptions.Any(lot => lot.IsActive),
            LotCostSnapshotAmount = x.LotConsumptions
                .Where(lot => lot.IsActive)
                .Sum(lot => (decimal?)lot.TotalCostSnapshot) ?? 0m,
            Quantity = x.Quantity,
            RevenueAmountVnd = x.MerchandiseOrderDetail.MerchandiseOrder.Currency == null
                || x.MerchandiseOrderDetail.MerchandiseOrder.Currency.Trim() == string.Empty
                || x.MerchandiseOrderDetail.MerchandiseOrder.Currency.ToUpper() == "VND"
                || !x.MerchandiseOrderDetail.MerchandiseOrder.ExchangeRate.HasValue
                || x.MerchandiseOrderDetail.MerchandiseOrder.ExchangeRate.Value <= 0
                    ? x.Quantity * x.MerchandiseOrderDetail.UnitPriceAgreed
                    : x.Quantity * x.MerchandiseOrderDetail.UnitPriceAgreed
                        * x.MerchandiseOrderDetail.MerchandiseOrder.ExchangeRate.Value,
            BaseCostAmount = x.Quantity * x.MerchandiseOrderDetail.BaseCostSnapshot
        });
    }
}
