using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.MaterialEquivalences;
using HRM.Application.Features.Warehouse.Dtos;
using HRM.Application.Features.Warehouse.Queries.GetStockAvailable;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.WareHouses;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using static HRM.Application.Tests.Features.PLM.SampleRequests.SampleWorkDecisionPostgresTests;

namespace HRM.Application.Tests.Features.DevAndQA;

public sealed class MaterialDemandPostgresTests(IsolatedGroupStoragePostgres postgres) : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task PlpuReadsOnlyRemainingActiveCompanyDemandAndShortageIsSortedBeforePaging()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        foreach (var type in new[] { typeof(Category), typeof(Material), typeof(Product), typeof(SampleRequest), typeof(MfgProductionOrder), typeof(WarehouseTempStock) })
            await CreateTable(connection, db.Model.FindEntityType(type)!);
        var user = new ProductionOrderCreationTests.TestUser { Roles = [ApplicationRoles.Production.PLPUUser] };
        var company = user.CompanyId!.Value;
        var category = Guid.NewGuid();
        var product = Guid.NewGuid();
        await Insert(connection, db.Model.FindEntityType(typeof(Category))!, new() { ["CategoryId"] = category, ["CompanyId"] = company, ["Types"] = "NVL", ["IsActive"] = true });
        await Insert(connection, db.Model.FindEntityType(typeof(Product))!, new() { ["ProductId"] = product, ["CompanyId"] = company, ["Name"] = "Output", ["ColourCode"] = "OUTPUT", ["IsActive"] = true });
        foreach (var code in new[] { "NVL_NH_009", "NVL_NH_010" })
            await Insert(connection, db.Model.FindEntityType(typeof(Material))!, new() { ["MaterialId"] = Guid.NewGuid(), ["ExternalId"] = code,
                ["Name"] = code + " name", ["CategoryId"] = category, ["CompanyId"] = company, ["IsActive"] = true, ["Unit"] = "kg" });
        var tempId = 0;
        async Task Seed(string orderCode, string materialCode, string status, decimal requested, decimal used, Guid scope, string reserveStatus = "Open")
        {
            await Insert(connection, db.Model.FindEntityType(typeof(MfgProductionOrder))!, new() {
                ["MfgProductionOrderId"] = Guid.NewGuid(), ["ProductId"] = product, ["CompanyId"] = scope, ["IsActive"] = true,
                ["ExternalId"] = orderCode, ["Status"] = status, ["RequiredDate"] = new DateTime(2026, 11, 15) });
            await Insert(connection, db.Model.FindEntityType(typeof(WarehouseTempStock))!, new() {
                ["TempId"] = ++tempId, ["CompanyId"] = scope, ["VaCode"] = orderCode, ["Code"] = materialCode,
                ["QtyRequest"] = requested, ["QtyUsed"] = used, ["ReserveStatus"] = reserveStatus });
        }
        await Seed("MFG_A", "NVL_NH_009", "Scheduling", 70, 20, company);
        await Seed("MFG_B", "NVL_NH_009", "Running", 30, 0, company);
        await Seed("MFG_C", "NVL_NH_010", "Scheduling", 50, 0, company);
        await Seed("MFG_DONE", "CLOSED", "Done", 500, 0, company);
        await Seed("MFG_FINISHED", "CLOSED", "Finished", 500, 0, company);
        await Seed("MFG_CANCELED", "CLOSED", "Canceled", 500, 0, company);
        await Seed("MFG_USED", "USED", "Running", 10, 20, company);
        await Seed("MFG_CLOSED_RESERVE", "CLOSED", "Running", 50, 0, company, "Cancelled");
        await Seed("MFG_OTHER", "NVL_NH_009", "Running", 999, 0, Guid.NewGuid());
        var sender = System.Reflection.DispatchProxy.Create<ISender, StockSender>();
        var handler = new GetMaterialDemandsQueryHandler(db, user, new CurrentUserPermissionService(user), sender);
        var result = await handler.Handle(new() { PageSize = 1 }, default);
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal("NVL_NH_010", Assert.Single(result.Items).Code);
        Assert.Equal(50, result.Items[0].ShortageKg);
        var page2 = await handler.Handle(new() { PageNumber = 2, PageSize = 1 }, default);
        var second = Assert.Single(page2!.Items);
        Assert.Equal("NVL_NH_009", second.Code);
        Assert.Equal(80, second.RemainingKg);
        Assert.Equal(0, second.ShortageKg);
        Assert.Equal(20, second.AvailableKg);
        Assert.Equal(2, second.Orders.Count);
        Assert.Equal(50, second.Orders.Single(x => x.Code == "MFG_A").RemainingKg);
        Assert.Null(second.DefectiveOnHandKg);
        user.HasExplicitPermissionSet = true;
        Assert.Null(await handler.Handle(new(), default));
        user.HasExplicitPermissionSet = false;
        user.Roles = [ApplicationRoles.Lab.LabUser];
        Assert.Null(await handler.Handle(new(), default));
    }

    public class StockSender : System.Reflection.DispatchProxy
    {
        protected override object? Invoke(System.Reflection.MethodInfo? method, object?[]? args)
        {
            if (args?[0] is not GetStockAvailableQuery request) throw new NotSupportedException();
            Assert.DoesNotContain(StockType.DefectiveRawMaterial, request.StockTypes);
            var stocks = new[] {
                new StockAvailableDto { Code = "NVL_NH_009", StockType = StockType.RawMaterial, TotalOnHandKg = 100, ReservedOpenAllKg = 80, AvailableKg = 20 },
                new StockAvailableDto { Code = "NVL_NH_010", StockType = StockType.RawMaterial, TotalOnHandKg = 20, ReservedOpenAllKg = 100, AvailableKg = -80 }
            }.Where(x => request.ExactCodes!.Contains(x.Code)).ToArray();
            return Task.FromResult(OperationResult<PagedResult<StockAvailableDto>>.Ok(new PagedResult<StockAvailableDto>(stocks, stocks.Length, 1, 100)));
        }
    }
}
