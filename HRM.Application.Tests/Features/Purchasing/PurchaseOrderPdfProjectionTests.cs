using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Features.Purchasing.PurchaseOrders.Queries.ExportPurchaseOrderPdf;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.OrderSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRM.Application.Tests.Features.Purchasing;

public sealed class PurchaseOrderPdfProjectionTests
{
    [Theory]
    [InlineData(false, "Chi Vy")]
    [InlineData(true, null)]
    public void LegacyContactMatchesDetailAndPdfWithoutOverwritingExistingSnapshot(bool hasSnapshot, string? expected)
    {
        var (db, store) = HRM.Application.Tests.Features.DevAndQA.IQC.IqcTestStore.CreateContext<IPurchaseOrderDbContext>();
        var company = Guid.NewGuid();
        var po = new HRM.Domain.Entities.OrderSchema.PurchaseOrder { PurchaseOrderId = Guid.NewGuid(), CompanyId = company, IsActive = true,
            Supplier = new() { CompanyId = company, SupplierContacts = [new() { FirstName = "Chi", LastName = "Vy", IsActive = true, IsPrimary = false }] },
            PurchaseOrderSnapshot = hasSnapshot ? new() { SupplierContactSnapshot = null } : null };
        store.Seed(nameof(db.PurchaseOrders), po);
        var pdf = new ExportPurchaseOrderPdfQueryHandler(db, null!, null!, null!, null!).BuildQuery(po.PurchaseOrderId, company).Single();
        var detail = new HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrderById.GetPurchaseOrderByIdQueryHandler(db, null!, null!).BuildQuery(po.PurchaseOrderId, company).Single();
        Assert.Equal(expected, pdf.SupplierContactSnapshot);
        Assert.Equal(expected, detail.SupplierContact);
        Assert.Null(po.PurchaseOrderSnapshot?.SupplierContactSnapshot);
    }
    [Fact]
    public void Pdf_query_translates_to_postgresql_with_company_scope()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var companyId = Guid.NewGuid();
        var handler = new ExportPurchaseOrderPdfQueryHandler(db, null!, null!, null!, null!);
        var sql = handler.BuildQuery(Guid.NewGuid(), companyId).ToQueryString();
        Assert.Contains(companyId.ToString(), sql);
        Assert.Contains("TotalPriceAgreed", sql);
        Assert.Contains("IsActive", sql);
    }
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(123)]
    public void Pdf_preserves_null_and_zero_amounts_and_snapshot_values(int? storedTotal)
    {
        var (db, store) = IqcTestStore.CreateContext<IPurchaseOrderDbContext>();
        var companyId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var po = new PurchaseOrder
        {
            PurchaseOrderId = id, CompanyId = companyId, IsActive = true,
            PurchaseOrderSnapshot = new()
            {
                TotalPrice = storedTotal, Vat = 8, SupplierNameSnapshot = "Historical supplier",
                SupplierAddressSnapshot = "Historical address"
            },
            PurchaseOrderDetails = [new()
            {
                PurchaseOrderId = id, IsActive = true, RequestQuantity = 50m, UnitPriceAgreed = 360000m,
                TotalPriceAgreed = storedTotal
            }, new() { PurchaseOrderId = id, IsActive = false, TotalPriceAgreed = 999999m }]
        };
        store.Seed(nameof(db.PurchaseOrders), po);
        var handler = new ExportPurchaseOrderPdfQueryHandler(db, null!, null!, null!, null!);
        var result = Assert.Single(handler.BuildQuery(id, companyId));
        Assert.Equal(storedTotal.HasValue ? (decimal?)storedTotal.Value : null, result.TotalPrice);
        Assert.Equal(storedTotal.HasValue ? (decimal?)storedTotal.Value : null, Assert.Single(result.Details).TotalUnitPriceAgreed);
        Assert.Equal("Historical supplier", result.SupplierNameSnapshot);
        Assert.Equal("Historical address", result.SupplierAddressSnapshot);
        Assert.Empty(handler.BuildQuery(id, Guid.NewGuid()));
        po.IsActive = false;
        Assert.Empty(handler.BuildQuery(id, companyId));
    }
}
