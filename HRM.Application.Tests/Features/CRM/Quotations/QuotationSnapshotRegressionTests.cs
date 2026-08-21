using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Infrastructure.Documents.Pdfs;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;
using System.Text.Json;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class QuotationSnapshotRegressionTests
{
    public QuotationSnapshotRegressionTests()
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;
    }

    [Fact]
    public void SnapshotAndPdf_DoNotChangeWhenNewPricingIsPublished()
    {
        var companyId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var source = ApprovedVersion(companyId, productId);
        var snapshotResult = QuotationPricingSnapshotFactory.Create(
            Guid.NewGuid(),
            companyId,
            productId,
            "USD",
            quantity: 75m,
            QuotationLinePriceMode.Tiered,
            source,
            "lines[0]");

        Assert.True(snapshotResult.Success);
        var snapshot = snapshotResult.Data!;
        var sentQuotation = QuotationFromSnapshot(companyId, productId, source, snapshot);
        var pdfSnapshot = PdfFromSnapshot(snapshot);
        var sentContentBefore = QuotationSentContentBuilder.Build(sentQuotation);
        var pdfInputBefore = JsonSerializer.SerializeToUtf8Bytes(pdfSnapshot);
        var renderedPdfBefore = CreateRenderer().Render(pdfSnapshot);

        source.Status = ProductPricingStatus.Superseded;
        source.Version = 2;
        source.StandardSellingPrice = 999m;
        source.PriceTiers.Single().UnitPrice = 999m;

        var sentContentAfter = QuotationSentContentBuilder.Build(sentQuotation);
        var pdfInputAfter = JsonSerializer.SerializeToUtf8Bytes(pdfSnapshot);
        var renderedPdfAfter = CreateRenderer().Render(pdfSnapshot);

        Assert.Equal(sentContentBefore, sentContentAfter);
        Assert.Equal(pdfInputBefore, pdfInputAfter);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(renderedPdfBefore, 0, 4));
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(renderedPdfAfter, 0, 4));
        Assert.Equal(120m, sentQuotation.Lines.Single().PriceTiers.Single().UnitPrice);
        Assert.Equal(120m, pdfSnapshot.Lines.Single().PriceTiers.Single().UnitPrice);
    }

    [Fact]
    public void Snapshot_RejectsWrongCurrencyOrNonApprovedVersion()
    {
        var companyId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var source = ApprovedVersion(companyId, productId);

        var wrongCurrency = QuotationPricingSnapshotFactory.Create(
            Guid.NewGuid(), companyId, productId, "EUR", 75m,
            QuotationLinePriceMode.Tiered, source, "lines[0]");
        source.Status = ProductPricingStatus.Draft;
        var draft = QuotationPricingSnapshotFactory.Create(
            Guid.NewGuid(), companyId, productId, "USD", 75m,
            QuotationLinePriceMode.Tiered, source, "lines[0]");

        Assert.False(wrongCurrency.Success);
        Assert.False(draft.Success);
    }

    private static ProductPricingVersion ApprovedVersion(Guid companyId, Guid productId)
        => new()
        {
            ProductPricingVersionId = Guid.NewGuid(),
            CompanyId = companyId,
            ProductId = productId,
            Currency = "USD",
            Status = ProductPricingStatus.Approved,
            IsActive = true,
            Version = 1,
            StandardSellingPrice = 120m,
            PriceTiers =
            [
                new ProductPricingTier
                {
                    ProductPricingTierId = Guid.NewGuid(),
                    QuantityRangeLabel = "All quantities",
                    MinInclusive = true,
                    MaxInclusive = true,
                    UnitPrice = 120m,
                    SortOrder = 0
                }
            ]
        };

    private static Quotation QuotationFromSnapshot(
        Guid companyId,
        Guid productId,
        ProductPricingVersion source,
        QuotationLinePricing snapshot)
        => new()
        {
            QuotationId = Guid.NewGuid(),
            ExternalId = "Q-SNAPSHOT-001",
            CompanyId = companyId,
            Currency = "USD",
            SentDate = new DateTime(2026, 8, 21),
            Lines =
            [
                new QuotationLine
                {
                    QuotationLineId = snapshot.PriceTiers.Single().QuotationLineId,
                    ProductId = productId,
                    ProductPricingVersionId = source.ProductPricingVersionId,
                    ProductExternalIdSnapshot = "P-001",
                    ProductNameSnapshot = "Snapshot product",
                    Quantity = 75m,
                    Unit = "kg",
                    PriceMode = QuotationLinePriceMode.Tiered,
                    UnitPrice = snapshot.EffectiveUnitPrice,
                    LineTotal = 75m * snapshot.EffectiveUnitPrice,
                    PriceTiers = snapshot.PriceTiers.ToList()
                }
            ]
        };

    private static QuotationPdfDocumentDto PdfFromSnapshot(QuotationLinePricing snapshot)
        => new()
        {
            ExternalId = "Q-SNAPSHOT-001",
            QuotationDate = new DateTime(2026, 8, 21),
            Currency = "USD",
            CompanyName = "Company",
            CustomerName = "Customer",
            TotalAmount = 75m * snapshot.EffectiveUnitPrice,
            Lines =
            [
                new QuotationPdfLineDto
                {
                    ProductCode = "P-001",
                    ProductName = "Snapshot product",
                    Quantity = 75m,
                    Unit = "kg",
                    PriceMode = QuotationLinePriceMode.Tiered,
                    UnitPrice = snapshot.EffectiveUnitPrice,
                    LineTotal = 75m * snapshot.EffectiveUnitPrice,
                    PriceTiers = snapshot.PriceTiers.Select(x => new QuotationPdfPriceTierDto
                    {
                        QuantityRangeLabel = x.QuantityRangeLabel,
                        UnitPrice = x.UnitPrice,
                        SortOrder = x.SortOrder
                    }).ToArray()
                }
            ]
        };

    private static QuotationPdfRenderer CreateRenderer()
        => new(Options.Create(new PdfOptions()), new FakeHostEnvironment());

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "HRM.Application.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
