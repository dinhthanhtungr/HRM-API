namespace HRM.Application.Abstractions.Persistence.Reports;

/// <summary>Read-only projection of existing purchase.v_po_ap_invoice_summary; no schema ownership.</summary>
public sealed class PLPUPoInvoiceSummaryRow
{
    public Guid CompanyId { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public string PoNo { get; set; } = string.Empty;
    public Guid? SourceSupplierId { get; set; }
    public string PoStatus { get; set; } = string.Empty;
    public long PoLineCount { get; set; }
    public decimal PoQty { get; set; }
    public decimal PoAmountExclVat { get; set; }
    public long InvoiceCount { get; set; }
    public string InvoiceList { get; set; } = string.Empty;
    public decimal ApQty { get; set; }
    public decimal ApAmountExclVat { get; set; }
    public decimal ApVatAmount { get; set; }
    public decimal ApAmountInclVat { get; set; }
    public decimal DiffQty { get; set; }
    public decimal DiffAmountExclVat { get; set; }
    public string ReconcileStatus { get; set; } = string.Empty;
}
