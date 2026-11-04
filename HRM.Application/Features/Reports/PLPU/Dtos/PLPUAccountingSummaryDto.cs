namespace HRM.Application.Features.Reports.PLPU;

/// <summary>Accounting cohort from the existing DB view, independent of the NVL cohort.</summary>
public sealed class PLPUAccountingSummaryDto
{
    public string Source => "purchase.v_po_ap_invoice_summary";
    public bool IncludesInactive => false;
    public int TotalPo { get; init; }
    public int SupplierCount { get; init; }
    public int PoWithoutSupplierCount { get; init; }
    public long TotalLines { get; init; }
    public decimal PoQuantity { get; init; }
    public decimal PoAmountExclVat { get; init; }
    public decimal? PoVatAmount { get; init; }
    public decimal? PoAmountInclVat { get; init; }
    public int MissingPoTotalCount { get; init; }
    public long LinkedInvoiceCount { get; init; }
    public int PoWithInvoiceCount { get; init; }
    public int PoWithoutInvoiceCount { get; init; }
    public int MatchedPoCount { get; init; }
    public int PartialPoCount { get; init; }
    public int OverPoCount { get; init; }
    public decimal InvoiceQuantity { get; init; }
    public decimal InvoiceAmountExclVat { get; init; }
    public decimal InvoiceVatAmount { get; init; }
    public decimal InvoiceAmountInclVat { get; init; }
    public decimal QuantityDifference { get; init; }
    public decimal AmountDifferenceExclVat { get; init; }
    public decimal UninvoicedPoAmountExclVat { get; init; }
    public decimal PoWithInvoicePercent { get; init; }
    public decimal MatchedPoPercent { get; init; }
    public IReadOnlyList<PLPUAccountingStatusCount> PoStatuses { get; init; } = [];
}

public sealed record PLPUAccountingStatusCount(string Status, int Count);
