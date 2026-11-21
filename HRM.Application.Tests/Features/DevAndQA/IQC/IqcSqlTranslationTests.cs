using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Application.Features.DevAndQA.IQC.Services;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.WareHouses;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRM.Application.Tests.Features.DevAndQA.IQC;

public sealed class IqcSqlTranslationTests
{
    [Theory]
    [InlineData("supplierCode: BB")]
    [InlineData("supplierName: BB")]
    public void SupplierSuggestionsUseJoinsInsteadOfPerRowLateralQueries(string keyword)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var reader = new IqcReader(db, Guid.NewGuid());
        var request = new QCInputQuery { IncludeLabTests = true, SuggestionKeyword = keyword };
        var rows = reader.BuildSmartRows(request);
        var sql = reader.BuildSmartSchema(rows).BuildSuggestionQuery(rows, request)!.ToQueryString();
        Assert.DoesNotContain("LATERAL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JOIN", sql);
        Assert.Contains("CompanyId", sql);
    }

    [Fact]
    public void SupplierSuggestionJoinsKeepScopedRowsCompanyAndActiveWarehouseRequests()
    {
        var (db, store) = IqcTestStore.CreateStore();
        var company = Guid.NewGuid();
        var other = Guid.NewGuid();
        var headers = Enumerable.Range(1, 4).Select(i => new WarehouseVoucher
        {
            VoucherId = i, CompanyId = i == 3 ? other : company, RequestId = i
        }).ToArray();
        var details = headers.Select(h => new WarehouseVoucherDetail
        {
            VoucherDetailId = 100 + h.VoucherId, VoucherId = h.VoucherId, Voucher = h
        }).ToArray();
        var requests = headers.Select(h => new WarehouseRequest
        {
            RequestId = (int)h.VoucherId, CompanyId = h.CompanyId,
            IsActive = h.VoucherId != 4, codeFromRequest = "PO" + h.VoucherId
        }).ToArray();
        var orders = headers.Select(h => new PurchaseOrder
        {
            PurchaseOrderId = Guid.NewGuid(), CompanyId = h.CompanyId, ExternalId = "PO" + h.VoucherId,
            Supplier = new Supplier { SupplierId = Guid.NewGuid(), CompanyId = h.VoucherId == 2 ? other : h.CompanyId }
        }).ToArray();
        store.Seed("WarehouseVoucherDetails", details);
        store.Seed("WarehouseRequests", requests);
        store.Seed("PurchaseOrders", orders);
        var scoped = details.Select(d => new IqcReader.SmartRow { VoucherDetailId = d.VoucherDetailId }).AsQueryable();
        var reader = new IqcReader(db, company);
        Assert.Equal(orders[0].PurchaseOrderId, Assert.Single(reader.BuildSmartSupplierOrders(scoped)).PurchaseOrderId);
        Assert.Empty(reader.BuildSmartSupplierOrders(scoped.Where(x => x.VoucherDetailId != 101)));
        Assert.Empty(reader.BuildSmartSupplierOrders(new[] { new IqcReader.SmartRow { LabTestId = Guid.NewGuid() } }.AsQueryable()));
    }

    [Theory]
    [InlineData("BB")]
    [InlineData("bm_295")]
    [InlineData("materialCode: NH_158")]
    public void SuggestionsWithoutDateFiltersDoNotAggregateWarehouseReceiptHistory(string keyword)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var reader = new IqcReader(db, Guid.NewGuid());
        var request = new QCInputQuery { IncludeLabTests = true, SuggestionKeyword = keyword };
        var rows = reader.BuildSmartRows(request);
        var sql = reader.BuildSmartSchema(rows).BuildSuggestionQuery(rows, request)!.ToQueryString();
        Assert.DoesNotContain("WarehouseShelfLedger", sql);
        Assert.Contains("CompanyId", sql);
        Assert.Contains("ILIKE", sql);
    }

    [Fact]
    public void MaterialCodeSuggestionsDoNotLookUpSuppliersWithoutSupplierFilters()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var reader = new IqcReader(db, Guid.NewGuid());
        var request = new QCInputQuery { IncludeLabTests = true, SuggestionKeyword = "materialCode: NH_158" };
        var rows = reader.BuildSmartRows(request);
        var sql = reader.BuildSmartSchema(rows).BuildSuggestionQuery(rows, request)!.ToQueryString();
        Assert.DoesNotContain("PurchaseOrder", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CompanyId", sql);
        Assert.Contains("ILIKE", sql);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void Suggestions_ReadOnlyMetadataAndKeepQcDateAndCompanyFilters(bool? hasQc)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var reader = new IqcReader(db, Guid.NewGuid());
        var request = new QCInputQuery {
            IncludeLabTests = true, SuggestionKeyword = "NH_158", HasQC = hasQc,
            FromDate = new(2026, 11, 1), ToDate = new(2026, 11, 30)
        };
        var rows = reader.BuildSmartRows(request);
        var sql = reader.BuildSmartSchema(rows).BuildSuggestionQuery(rows, request)!.ToQueryString();
        Assert.Contains("CompanyId", sql);
        Assert.Contains("WarehouseShelfLedger", sql);
        if (hasQc != false) Assert.Contains("TestDate", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("JOIN hr.", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Categories", sql, StringComparison.OrdinalIgnoreCase);
        if (hasQc.HasValue) Assert.Contains("QCInputByQC", sql);
        else Assert.DoesNotContain("QCInputByQC", sql);
        Assert.Equal(7, System.Text.RegularExpressions.Regex.Matches(sql, "LIMIT @").Count);
    }

    [Fact]
    public void LabBatchBadgeUsesTheSameLotAsTheDisplayedRowWithinCompany()
    {
        var (context, store) = IqcTestStore.CreateStore();
        var company = Guid.NewGuid();
        store.Seed("LabTests",
            new HRM.Domain.Entities.DevandqaSchema.LabTest { CompanyId = company, Code = "LAB-A", SampleName = "Sample", BatchNumber = "LOT_158" },
            new HRM.Domain.Entities.DevandqaSchema.LabTest { CompanyId = Guid.NewGuid(), Code = "LAB-B", SampleName = "Sample", BatchNumber = "LOT_158" });
        var reader = new IqcReader(context, company);
        var rows = reader.BuildSmartRows(new QCInputQuery { IncludeLabTests = true, SuggestionKeyword = "Số lô: LOT" });
        var selected = reader.BuildSmartSchema(rows).Apply(rows, null,
            "[{\"field\":\"batchNumber\",\"value\":\"LOT_158\"}]", out _).ToArray();
        Assert.Equal("LAB-A", Assert.Single(selected).Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void ListAndReportSelectors_TranslateWithAndWithoutQcFilter(bool? hasQc)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var reader = new IqcReader(db, Guid.NewGuid());
        var filter = new QCInputQuery { HasQC = hasQc, Keyword = "MAT", FromDate = new(2026, 10, 1), ToDate = new(2026, 10, 31), VoucherDetailType = VoucherDetailType.Waiter };
        var idsSql = reader.BuildSummaryIdsQuery(filter).Skip(10).Take(15).ToQueryString();
        Assert.Contains("ILIKE", idsSql);
        Assert.Contains("companyid", idsSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", idsSql);
        Assert.Contains("WarehouseShelfLedger", idsSql);
        Assert.Contains("max(", idsSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("createdAt", idsSql);
        Assert.Contains("LEFT JOIN", idsSql);

        Assert.Contains("companyid", reader.BuildSummaryItemsQuery(new long[] { 1, 2 }).ToQueryString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("companyid", reader.BuildExportSummaryItemsQuery(new long[] { 1, 2 }).ToQueryString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DetailWithLatestQcAndSupplier_TranslatesToScopedPostgresSql()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = new IqcReader(db, Guid.NewGuid()).BuildDetailQuery(1).ToQueryString();
        Assert.Contains("LIMIT 1", sql);
        Assert.Contains("companyid", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("createddate", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TrackingDetailScopesIncomingRowsAndOmitsSensitiveFields()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = new IqcReader(db, Guid.NewGuid()).BuildDetailQuery(1, trackingOnly: true).ToQueryString();

        Assert.Contains("CompanyId", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VoucherType", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch("""(?i)\.(?:"(?:inspectionMethod|attachmentCollectionId|attachmentLastError|note)"|(?:inspectionMethod|attachmentCollectionId|attachmentLastError|note)(?![a-z]))""", sql);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExportFiltersAndNullQc_TranslateToPostgres(bool hasQc)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = new IqcReader(db, Guid.NewGuid()).BuildExportRowsQuery(new QCInputQuery
        {
            HasQC = hasQc, Keyword = "MAT", FromDate = new(2026, 1, 1), ToDate = new(2026, 12, 31),
            VoucherDetailType = VoucherDetailType.Special
        }).ToQueryString();
        Assert.Contains("ILIKE", sql);
        Assert.Contains("companyid", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CASE", sql);
        Assert.Contains("WarehouseShelfLedger", sql);
        Assert.Contains("max(", sql, StringComparison.OrdinalIgnoreCase);
    }
}
