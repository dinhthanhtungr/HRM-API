using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Materials.Dtos;
using HRM.Application.Features.PLM.Materials.Queries.GetSupplierDetail;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.WareHouses;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRM.Application.Tests.Features.Purchasing;

public sealed class SupplierActualDeliveryTests
{
    [Fact]
    public async Task FirstActualDeliveryIsStable_AndDuplicateMaterialLinesCountOnce()
    {
        var (db, store) = IqcTestStore.CreateContext<IPLMReadDbContext>();
        var company = Guid.NewGuid();
        var material = new Material { MaterialId = Guid.NewGuid(), CompanyId = company, IsActive = true };
        var supplier = new Supplier { SupplierId = Guid.NewGuid(), CompanyId = company, IsActive = true };
        var start = new DateTime(2026, 10, 1, 8, 0, 0);
        var po = new PurchaseOrder { PurchaseOrderId = Guid.NewGuid(), ExternalId = "DDH01", CompanyId = company, IsActive = true, Status = "InProgress", CreateDate = start };
        po.PurchaseOrderDetails = [new() { MaterialId = material.MaterialId, Material = material, IsActive = true }, new() { MaterialId = material.MaterialId, Material = material, IsActive = true }];
        var missing = new PurchaseOrder { PurchaseOrderId = Guid.NewGuid(), ExternalId = "DDH02", CompanyId = company, IsActive = true, Status = "Completed", CreateDate = start };
        var invalid = new PurchaseOrder { PurchaseOrderId = Guid.NewGuid(), ExternalId = "DDH03", CompanyId = company, IsActive = true, Status = "Completed", CreateDate = start };
        supplier.PurchaseOrders = [po, missing, invalid,
            new() { PurchaseOrderId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true, Status = "Completed" },
            new() { PurchaseOrderId = Guid.NewGuid(), CompanyId = company, IsActive = false, Status = "Completed" },
            new() { PurchaseOrderId = Guid.NewGuid(), CompanyId = company, IsActive = true, Status = "Cancelled" }];
        store.Seed(nameof(db.Suppliers), supplier);
        store.Seed(nameof(db.QCInputByQCs), Inspection("DDH01", company, start.AddDays(7)),
            Inspection(" DDH01 ", company, start.AddDays(2)),
            Inspection("DDH01", Guid.NewGuid(), start.AddDays(1)),
            Inspection("DDH01", company, start.AddDays(1), false),
            Inspection("DDH03", company, start.AddDays(-1)),
            Inspection("DDH03", company, start.AddDays(3)));
        var dto = new SupplierDetailDto { Materials = new PagedResult<SupplierMaterialDto>([new() { MaterialId = material.MaterialId }], 1, 1, 15) };
        await SupplierActualDeliveryReader.PopulateAsync(db, company, supplier.SupplierId, dto, default);
        Assert.Equal(2d, dto.ActualDelivery.AverageDays);
        Assert.Equal(3, dto.ActualDelivery.OrderCount);
        Assert.Equal(1, dto.ActualDelivery.SampleCount);
        Assert.Equal(1, dto.ActualDelivery.MissingDateCount);
        Assert.Equal(1, dto.ActualDelivery.InvalidDateCount);
        var perMaterial = Assert.Single(dto.Materials.Items).ActualDelivery;
        Assert.Equal(2d, perMaterial.AverageDays);
        Assert.Equal(1, perMaterial.SampleCount);
        Assert.Empty(await SupplierActualDeliveryReader.Query(db, Guid.NewGuid(), supplier.SupplierId).ToListAsync());
    }

    [Fact]
    public void Average_ExcludesMissingAndInvalidDates_AndKeepsSameDayZero()
    {
        var start = new DateTime(2026, 10, 1);
        var values = new[]
        {
            new SupplierActualDeliveryReader.Observation { CreatedAt = start, FirstActualDeliveryAt = start },
            new SupplierActualDeliveryReader.Observation { CreatedAt = start, FirstActualDeliveryAt = start.AddDays(5) },
            new SupplierActualDeliveryReader.Observation { CreatedAt = null, FirstActualDeliveryAt = start },
            new SupplierActualDeliveryReader.Observation { CreatedAt = start, FirstActualDeliveryAt = start.AddDays(-1) }
        };
        var result = SupplierActualDeliveryReader.Aggregate(values.AsQueryable()).Single();
        Assert.Equal(2.5d, result.AverageDays);
        Assert.Equal(2, result.SampleCount);
        var missing = SupplierActualDeliveryReader.Aggregate(values.Skip(2).Take(1).AsQueryable()).Single();
        Assert.Null(missing.AverageDays);
    }

    [Fact]
    public async Task MaterialSupplierMetrics_AreScopedPerSupplierMaterialAndCompany()
    {
        var (db, store) = IqcTestStore.CreateContext<IPLMReadDbContext>();
        var company = Guid.NewGuid();
        var material = new Material { MaterialId = Guid.NewGuid(), CompanyId = company };
        var start = new DateTime(2026, 10, 1);
        PurchaseOrder Order(string code, params PurchaseOrderDetail[] details) => new()
        {
            PurchaseOrderId = Guid.NewGuid(), ExternalId = code, CompanyId = company,
            IsActive = true, CreateDate = start, PurchaseOrderDetails = details.ToList()
        };
        PurchaseOrderDetail Line() => new() { MaterialId = material.MaterialId, Material = material, IsActive = true };
        var supplier = new Supplier
        {
            SupplierId = Guid.NewGuid(), CompanyId = company, IsActive = true,
            PurchaseOrders = [Order("FIRST", Line(), Line()), Order("MISSING", Line()), Order("OTHER")]
        };
        var inactive = new Supplier
        {
            SupplierId = Guid.NewGuid(), CompanyId = company, IsActive = false,
            PurchaseOrders = [Order("INACTIVE", Line())]
        };
        var foreign = new Supplier
        {
            SupplierId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true,
            PurchaseOrders = [Order("FOREIGN", Line())]
        };
        store.Seed(nameof(db.Suppliers), supplier, inactive, foreign);
        store.Seed(nameof(db.QCInputByQCs), Inspection("FIRST", company, start.AddDays(2.125)),
            Inspection("FIRST", company, start.AddDays(8)), Inspection("OTHER", company, start.AddDays(30)),
            Inspection("INACTIVE", company, start.AddDays(4)), Inspection("FOREIGN", company, start.AddDays(1)));
        var ids = new[] { supplier.SupplierId, inactive.SupplierId, foreign.SupplierId };
        var rows = await SupplierActualDeliveryReader.QueryForMaterial(db, company, material.MaterialId, ids, false).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, x => Assert.Equal(supplier.SupplierId, x.SupplierId));
        var metric = SupplierActualDeliveryReader.Aggregate(rows.AsQueryable()).Single();
        SupplierActualDeliveryReader.Round(metric);
        Assert.Equal(2.13, metric.AverageDays);
        Assert.Equal(2, metric.OrderCount);
        Assert.Equal(1, metric.SampleCount);
        Assert.Equal(1, metric.MissingDateCount);
        var history = await SupplierActualDeliveryReader.QueryForMaterial(db, company, material.MaterialId, ids, true).ToListAsync();
        Assert.Equal(3, history.Count);
        Assert.Single(history, x => x.SupplierId == inactive.SupplierId);
        Assert.Empty(await SupplierActualDeliveryReader.QueryForMaterial(db, company, Guid.NewGuid(), ids, true).ToListAsync());
    }

    [Fact]
    public void IqcDateAndAggregation_TranslateToPostgres()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var query = SupplierActualDeliveryReader.Query(db, Guid.NewGuid(), Guid.NewGuid());
        var sql = SupplierActualDeliveryReader.Aggregate(query).ToQueryString();
        Assert.Contains("MIN(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("avg(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("QCInputByQC", sql);
        Assert.Contains("CompanyId", sql);
        Assert.DoesNotContain("UpdatedDate", sql);
        Assert.DoesNotContain("AuditLogs", sql);
        var materialSql = SupplierActualDeliveryReader.QueryForMaterial(db, Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()], true).ToQueryString();
        Assert.Contains("EXISTS", materialSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MIN(", materialSql, StringComparison.OrdinalIgnoreCase);
    }

    private static QCInputByQC Inspection(string code, Guid companyId, DateTime at, bool active = true) => new()
    {
        CreatedDate = at,
        WarehouseVoucherDetail = new WarehouseVoucherDetail
        {
            Voucher = new WarehouseVoucher
            {
                CompanyId = companyId, VoucherType = (int)WareHouseRequestType.ImportOther,
                WarehouseRequest = new WarehouseRequest { CompanyId = companyId, IsActive = active, codeFromRequest = code }
            }
        }
    };
}
