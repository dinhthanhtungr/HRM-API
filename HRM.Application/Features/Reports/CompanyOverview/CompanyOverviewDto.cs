using HRM.Application.Features.Reports.PLPU;
using HRM.Application.Features.Reports.Sales.Queries.GetSalesReportHeader;

namespace HRM.Application.Features.Reports.CompanyOverview;

public sealed class CompanyOverviewDto
{
    public Guid CompanyId { get; init; }
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string Currency => "VND";
    public required PLPUPurchaseOverviewSummaryDto RawMaterials { get; init; }
    public required SalesReportHeaderDto Sales { get; init; }
    public required CompanyPurchasesDto Purchases { get; init; }
    public IReadOnlyList<CompanyStockDto> CurrentStock { get; init; } = [];
    public IReadOnlyList<CompanyMonthlyPurchasesDto> MonthlyPurchases { get; init; } = [];
}

public sealed record CompanyMonthlyPurchasesDto(DateTime Month, decimal RawMaterialsReceivedValue,
    decimal KnownSuppliesOrderedValue, int SuppliesAmountIssueCount);

public sealed class CompanyPurchasesDto
{
    public IReadOnlyList<CompanyPurchaseGroupDto> Groups { get; init; } = [];
    public int IssueCount { get; init; }
    // Bounded preview; IssueCount is the full count, not the preview length.
    public IReadOnlyList<CompanyPurchaseIssueDto> Issues { get; init; } = [];
}

public sealed class CompanyPurchaseGroupDto
{
    public required string Kind { get; init; }
    public int OrderCount { get; init; }
    public decimal KnownOrderedAmount { get; init; }
    public string OrderedAmountSource => Kind == "Supplies"
        ? "purchase.v_po_ap_invoice_item_reconcile.po_amount_excl_vat"
        : "Orders.PurchaseOrderDetails.TotalPriceAgreed";
    public int OrderedAmountIssueCount { get; init; }
    public int ReceiptCount { get; init; }
    public int ReceiptLineCount { get; init; }
    public decimal KnownReceivedAmount { get; init; }
    public int ReceivedAmountIssueCount { get; init; }
    public bool IsOrderedAmountComplete => OrderedAmountIssueCount == 0;
    public bool IsReceivedAmountComplete => ReceivedAmountIssueCount == 0;
    public IReadOnlyList<CompanyPurchaseQuantityDto> ReceivedQuantities { get; init; } = [];
}

public sealed record CompanyPurchaseQuantityDto(string Unit, decimal Quantity);
public sealed record CompanyStockDto(string StockType, decimal QuantityKg);
public sealed record CompanyPurchaseIssueDto(
    string Source, string DocumentCode, string ItemCode, DateTime Date, string Reason);

// Read model shared only by this report's reader and calculator; never serialized directly.
public sealed class CompanyPurchaseFact
{
    public string Source { get; set; } = "";
    public string DocumentKey { get; set; } = "";
    public string DocumentCode { get; set; } = "";
    public Guid PurchaseOrderId { get; set; }
    public string ItemCode { get; set; } = "";
    public string Kind { get; set; } = "Unclassified";
    public DateTime Date { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "";
    public decimal? Amount { get; set; }
    public string? Issue { get; set; }
    public bool IncludeQuantity { get; set; } = true;
}
