using HRM.Application.Abstractions.Persistence.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.Reports;

public sealed class PLPUPoInvoiceSummaryConfiguration : IEntityTypeConfiguration<PLPUPoInvoiceSummaryRow>
{
    public void Configure(EntityTypeBuilder<PLPUPoInvoiceSummaryRow> builder)
    {
        builder.HasNoKey().ToView("v_po_ap_invoice_summary", "purchase");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.PurchaseOrderId).HasColumnName("purchase_order_id");
        builder.Property(x => x.PoNo).HasColumnName("po_no");
        builder.Property(x => x.SourceSupplierId).HasColumnName("source_supplier_id");
        builder.Property(x => x.PoStatus).HasColumnName("po_status");
        builder.Property(x => x.PoLineCount).HasColumnName("po_line_count");
        builder.Property(x => x.PoQty).HasColumnName("po_qty");
        builder.Property(x => x.PoAmountExclVat).HasColumnName("po_amount_excl_vat");
        builder.Property(x => x.InvoiceCount).HasColumnName("invoice_count");
        builder.Property(x => x.InvoiceList).HasColumnName("invoice_list");
        builder.Property(x => x.ApQty).HasColumnName("ap_qty");
        builder.Property(x => x.ApAmountExclVat).HasColumnName("ap_amount_excl_vat");
        builder.Property(x => x.ApVatAmount).HasColumnName("ap_vat_amount");
        builder.Property(x => x.ApAmountInclVat).HasColumnName("ap_amount_incl_vat");
        builder.Property(x => x.DiffQty).HasColumnName("diff_qty");
        builder.Property(x => x.DiffAmountExclVat).HasColumnName("diff_amount_excl_vat");
        builder.Property(x => x.ReconcileStatus).HasColumnName("reconcile_status");
    }
}
