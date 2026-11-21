using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Application.Features.DevAndQA.IQC.Services;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.DevAndQA.IQC;

public sealed class IqcSmartSupplierSuggestionTests
{
    [Theory]
    [InlineData("NCC")]
    [InlineData("supplierCode: NCC")]
    [InlineData("supplierName: nhua")]
    public void SupplierSuggestionQueryUsesJoinsWithoutPerRowLateralScans(string keyword)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql().Options);
        var company = Guid.NewGuid();
        var reader = new IqcReader(db, company);
        var request = new QCInputQuery { IncludeLabTests = true, SuggestionKeyword = keyword };
        var rows = reader.BuildSmartRows(request);
        var sql = reader.BuildSmartSchema(rows).BuildSuggestionQuery(rows, request)!.ToQueryString();

        Assert.DoesNotContain("LATERAL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PurchaseOrders", sql);
        Assert.Contains("WarehouseRequest", sql);
        Assert.Contains("Suppliers", sql);
        Assert.Contains(company.ToString(), sql);
        Assert.Contains("INNER JOIN", sql);
    }

    [Fact]
    public void SupplierSourceKeepsCompanyActiveRequestAndCurrentRowFilters()
    {
        var (reader, rows) = Fixture();
        var values = reader.BuildSmartSupplierOrders(rows)
            .Select(x => x.Supplier!.ExternalId).Distinct().Order().ToArray();
        Assert.Equal(new[] { "NCC_A", "NCC_B", "NCC_C" }, values);

        var filtered = rows.Where(x => x.LotNumber == "LOT_B");
        Assert.Equal(new[] { "NCC_C" }, reader.BuildSmartSupplierOrders(filtered)
            .Select(x => x.Supplier!.ExternalId).Distinct().ToArray());
        Assert.Empty(reader.BuildSmartSupplierOrders(rows.Where(x => x.LabTestId != null)));
        Assert.Empty(reader.BuildSmartSupplierOrders(rows.Where(x => false)));
    }

    [Fact]
    public void SupplierBadgeOrAndOtherFieldAndStillApplyBeforeSuggestionValues()
    {
        var (reader, rows) = Fixture();
        var schema = reader.BuildSmartSchema(rows);
        var selected = schema.Apply(rows, null,
            """
            [{"field":"supplierCode","value":"NCC_A"},
             {"field":"supplierCode","value":"NCC_C"},
             {"field":"batchNumber","value":"LOT_B"}]
            """, out _);

        Assert.Equal(2, Assert.Single(selected).VoucherDetailId);
        Assert.Equal("NCC_C", Assert.Single(reader.BuildSmartSupplierOrders(selected)).Supplier!.ExternalId);
    }

    [Fact]
    public void SupplierSourceKeepsAllExistingOrderMatchesWithoutAddingActiveOrderRestrictions()
    {
        var (reader, rows) = Fixture();
        var orders = reader.BuildSmartSupplierOrders(rows.Where(x => x.VoucherDetailId == 1)).ToArray();
        Assert.Equal(2, orders.Length);
        Assert.Contains(orders, x => x.IsActive == false && x.Supplier!.ExternalId == "NCC_B");
    }

    private static (IqcReader Reader, IQueryable<IqcReader.SmartRow> Rows) Fixture()
    {
        var (db, store) = IqcTestStore.CreateStore();
        var company = Guid.NewGuid();
        var otherCompany = Guid.NewGuid();
        Supplier Supplier(string code, Guid owner) => new() {
            SupplierId = Guid.NewGuid(), CompanyId = owner, ExternalId = code, SupplierName = code
        };
        var supplierA = Supplier("NCC_A", company);
        var supplierB = Supplier("NCC_B", company);
        var supplierC = Supplier("NCC_C", company);
        var foreignSupplier = Supplier("NCC_FOREIGN", otherCompany);
        PurchaseOrder Order(string code, Supplier supplier, Guid owner, bool active = true) => new() {
            PurchaseOrderId = Guid.NewGuid(), ExternalId = code, CompanyId = owner,
            SupplierId = supplier.SupplierId, Supplier = supplier, IsActive = active
        };
        store.Seed("PurchaseOrders",
            Order("PO_A", supplierA, company), Order("PO_A", supplierB, company, false),
            Order("PO_B", supplierC, company), Order("PO_A", foreignSupplier, company),
            Order("PO_A", supplierA, otherCompany), Order("PO_INACTIVE", supplierA, company),
            Order("PO_FOREIGN", supplierA, company));
        store.Seed("WarehouseRequests",
            new WarehouseRequest { RequestId = 1, CompanyId = company, codeFromRequest = "PO_A" },
            new WarehouseRequest { RequestId = 2, CompanyId = company, codeFromRequest = "PO_B" },
            new WarehouseRequest { RequestId = 3, CompanyId = company, codeFromRequest = "PO_INACTIVE", IsActive = false },
            new WarehouseRequest { RequestId = 4, CompanyId = otherCompany, codeFromRequest = "PO_FOREIGN" });
        WarehouseVoucherDetail Detail(long id, int? requestId, Guid owner) => new() {
            VoucherDetailId = id, VoucherId = id,
            Voucher = new WarehouseVoucher { VoucherId = id, CompanyId = owner, RequestId = requestId }
        };
        store.Seed("WarehouseVoucherDetails", Detail(1, 1, company), Detail(2, 2, company),
            Detail(3, 3, company), Detail(4, 4, company), Detail(5, 1, otherCompany), Detail(6, null, company));
        var rows = Enumerable.Range(1, 6).Select(id => new IqcReader.SmartRow {
            VoucherDetailId = id, LotNumber = id == 2 ? "LOT_B" : "LOT_A"
        }).Append(new IqcReader.SmartRow { LabTestId = Guid.NewGuid() }).AsQueryable();
        return (new IqcReader(db, company), rows);
    }
}
