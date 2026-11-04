namespace HRM.Application.Features.Purchasing.PurchaseOrders.Services;

/// <summary>VAT theo quy tắc tạo PO; tổng cuối khớp precision 18,2 của snapshot lưu DB.</summary>
public static class PurchaseOrderTotals
{
    public static decimal VatAmount(decimal subtotal, int? vat) =>
        decimal.Round(subtotal * (vat ?? 0) / 100m, 6, MidpointRounding.AwayFromZero);

    public static decimal Calculate(decimal subtotal, int? vat) =>
        decimal.Round(subtotal + VatAmount(subtotal, vat), 2, MidpointRounding.AwayFromZero);
}
