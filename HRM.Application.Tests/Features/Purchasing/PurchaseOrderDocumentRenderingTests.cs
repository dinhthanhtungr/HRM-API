using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Infrastructure.Documents.Pdfs;
using HRM.Infrastructure.Documents.Excels;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;
using ClosedXML.Excel;
using Xunit;
namespace HRM.Application.Tests.Features.Purchasing;

public sealed class PurchaseOrderDocumentRenderingTests
{
    [Theory]
    [InlineData(null, 19440000)]
    [InlineData(0, 0)]
    [InlineData(123, 123)]
    public void Pdf_totals_use_null_only_fallback_and_preserve_persisted_zero(int? persistedTotal, int expected)
    {
        var document = new PurchaseOrderPdfDocument
        {
            TotalPrice = persistedTotal, Vat = 8,
            Details = [new() { RequestQuantity = 50m, UnitPriceAgreed = 360000m, TotalUnitPriceAgreed = 18000000m }]
        };
        var totals = PurchaseOrderPdfRenderer.CalculateTotals(document);
        Assert.Equal(18000000m, totals.Subtotal);
        Assert.Equal(1440000m, totals.VatAmount);
        Assert.Equal((decimal)expected, totals.GrandTotal);
    }

    [Fact]
    public void Pdf_totals_preserve_line_zero_and_use_shared_money_rounding()
    {
        var document = new PurchaseOrderPdfDocument
        {
            Vat = 8,
            Details = [new() { TotalUnitPriceAgreed = 0m, RequestQuantity = 50m, UnitPriceAgreed = 360000m },
                new() { TotalUnitPriceAgreed = 1.23445m }, new() { RequestQuantity = null, UnitPriceAgreed = null }]
        };
        var totals = PurchaseOrderPdfRenderer.CalculateTotals(document);
        Assert.Equal(1.23445m, totals.Subtotal);
        Assert.Equal(0.098756m, totals.VatAmount);
        Assert.Equal(1.33m, totals.GrandTotal);
    }
    [Theory]
    [InlineData(2)]
    [InlineData(40)]
    public void Pdf_RendersLegacyForm_WithShortAndMultipageOrders(int count)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;
        using var font = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "OpenSans-Regular.ttf"));
        FontManager.RegisterFont(font);
        var dto = new PurchaseOrderPdfDocument
        {
            PurchaseOrderExternalIdSnapshot = "DDH-2610-0001",
            CreatedDate = new(2026, 10, 12),
            SupplierNameSnapshot = "Nhà cung cấp thử nghiệm",
            SupplierAddressSnapshot = "Địa chỉ nhà cung cấp",
            SupplierContactSnapshot = "Người liên hệ",
            SupplierPhoneNumberSnapshot = "0123456789",
            EmployeeFullNameSnapshot = "Người lập đơn",
            PhoneNumberSnapshot = "0123456789",
            Vat = 10,
            RequestDeliveryDate = new(2026, 10, 20),
            DeliveryAddress = "Kho Việt Úc",
            PaymentTypes = "Chuyển khoản trong 30 ngày",
            Note = "Giao đúng nguyên liệu và đầy đủ chứng từ.",
            TotalPrice = count * 137.5m,
            Details = Enumerable.Range(1, count).Select(i => new PurchaseOrderPdfLine
            {
                MaterialNameSnapshot = $"Nguyên liệu số {i}",
                RequestQuantity = 25m,
                UnitPriceAgreed = 5m,
                TotalUnitPriceAgreed = 125m,
                Package = "1 bao / 25 kg"
            }).ToList()
        };
        var pdf = new PurchaseOrderPdfRenderer(Options.Create(new PdfOptions()), new Host()).Render(dto);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        var output = Environment.GetEnvironmentVariable("PURCHASING_PDF_QA_DIR");
        if (!string.IsNullOrWhiteSpace(output)) { Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output, $"purchase-order-{count}.pdf"), pdf); File.WriteAllText(Path.Combine(output, $"purchase-order-{count}.json"), System.Text.Json.JsonSerializer.Serialize(dto)); }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pdf_renders_template_and_legacy_nullable_amount_fallback(bool template)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;
        using var font = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "OpenSans-Regular.ttf"));
        FontManager.RegisterFont(font);
        var renderer = new PurchaseOrderPdfRenderer(Options.Create(new PdfOptions()), new Host());
        var document = new PurchaseOrderPdfDocument
        {
            CreatedDate = new(2026, 10, 12), PurchaseOrderExternalIdSnapshot = "DDH261000102",
            SupplierNameSnapshot = "Nhà cung cấp thử nghiệm", Vat = 8, TotalPrice = null,
            Details = [new() { MaterialNameSnapshot = "Bột màu đỏ - Red", RequestQuantity = 50m,
                UnitPriceAgreed = 360000m, TotalUnitPriceAgreed = null, Package = "Bao 25 (Kg)" }]
        };
        var pdf = template ? renderer.RenderTemplate() : renderer.Render(document);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        var output = Environment.GetEnvironmentVariable("PURCHASING_PDF_QA_DIR");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output, template ? "purchase-order-template.pdf" : "purchase-order-fallback.pdf"), pdf);
            File.WriteAllText(Path.Combine(output, template ? "purchase-order-template.json" : "purchase-order-fallback.json"), System.Text.Json.JsonSerializer.Serialize(document));
        }
    }
    [Fact]
    public void ExcelList_RetainsTwentyColumnsAndWarehouseReceiptAmount()
    {
        var bytes = new PurchaseOrderExcelRenderer().RenderList([new PurchaseOrderExcelRow
        {
            PurchaseOrderCode = "DDH-1", OrderedQuantity = 10m, ReceivedQuantity = 10m, AcceptedQuantity = 6m,
            PendingQcQuantity = 3m, RejectedQuantity = 1m, UnitPrice = 100m, RealTotalAmount = 1000m
        }]);
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("Purchase Orders");
        Assert.Equal("Số lượng QC đạt", sheet.Cell(1, 16).GetString());
        Assert.Equal("Thành tiền thực nhận", sheet.Cell(1, 20).GetString());
        Assert.Equal(6m, sheet.Cell(2, 16).GetValue<decimal>());
        Assert.Equal(3m, sheet.Cell(2, 17).GetValue<decimal>());
        Assert.Equal(1m, sheet.Cell(2, 18).GetValue<decimal>());
        Assert.Equal(1000m, sheet.Cell(2, 20).GetValue<decimal>());
    }
    private sealed class Host : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
