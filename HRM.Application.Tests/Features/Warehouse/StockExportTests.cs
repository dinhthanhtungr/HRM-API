using System.Reflection;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Warehouse.Dtos;
using HRM.Application.Features.Warehouse.Queries.ExportStockAvailable;
using HRM.Application.Features.Warehouse.Queries.GetStockAvailable;
using HRM.Domain.Enums.WareHouses;
using MediatR;
using Xunit;
using HRM.Application.Commons.Authorization;
using HRM.Infrastructure.Documents.Excels;
using ClosedXML.Excel;

namespace HRM.Application.Tests.Features.Warehouse;

public sealed class StockExportTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuthenticatedCompanyRequiredBeforeQuerying(bool authenticated)
    {
        var sender = DispatchProxy.Create<ISender, Sender>();
        var user = new User(authenticated, authenticated ? null : Guid.NewGuid());
        var result = await new ExportStockAvailableQueryHandler(sender, user, new Renderer()).Handle(new(new()), default);
        Assert.False(result.Success);
        Assert.Null(((Sender)sender).Query);
    }

    [Fact]
    public async Task UsesExistingScopedQueryAndAppliedFiltersWithInternalUnpagedFlag()
    {
        var sender = DispatchProxy.Create<ISender, Sender>();
        var renderer = new Renderer();
        var result = await new ExportStockAvailableQueryHandler(sender, new User(true, Guid.NewGuid()), renderer)
            .Handle(new(new() { Keyword = "ITEM", StockTypes = [StockType.DefectiveFinishedGood],
                PageNumber = 5, PageSize = 15, AvailableMax = 20, OnlyAvailableLeZero = true }), default);
        Assert.True(result.Success);
        var query = ((Sender)sender).Query!;
        Assert.True(query.ExportAll);
        Assert.Equal("ITEM", query.Keyword);
        Assert.Equal(StockType.DefectiveFinishedGood, Assert.Single(query.StockTypes));
        Assert.Equal(20m, query.AvailableMax);
        Assert.True(query.OnlyAvailableLeZero);
        Assert.Equal(22, renderer.Count);
        Assert.False(typeof(GetStockAvailableQuery).GetProperty("ExportAll", BindingFlags.Public | BindingFlags.Instance)?.CanWrite ?? false);
    }

    private sealed class Renderer : IStockAvailableExcelRenderer
    {
        public int Count;
        public StockAvailableExportVisibility? Visibility;
        public byte[] Render(IReadOnlyList<StockAvailableDto> items, StockAvailableExportVisibility visibility, CancellationToken ct)
        { Count = items.Count; Visibility = visibility; return [1]; }
    }

    public class Sender : DispatchProxy
    {
        public GetStockAvailableQuery? Query;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Query = Assert.IsType<GetStockAvailableQuery>(args![0]);
            var items = Enumerable.Range(0, 22).Select(i => new StockAvailableDto { Code = $"ITEM-{i}" }).ToList();
            return Task.FromResult(OperationResult<PagedResult<StockAvailableDto>>.Ok(new PagedResult<StockAvailableDto>(items, 22, 1, 22)));
        }
    }

    private sealed record User(bool IsAuthenticated, Guid? CompanyId) : ICurrentUser
    {
        public Guid UserId => Guid.Empty;
        public Guid? EmployeeId => null;
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; init; } = [];
        public bool IsInRole(string role) => false;
    }

    [Theory]
    [InlineData(ApplicationRoles.Production.PLPUUser, true)]
    [InlineData(ApplicationRoles.Developer, true)]
    [InlineData(ApplicationRoles.President, false)]
    [InlineData(ApplicationRoles.Admin, false)]
    [InlineData(ApplicationRoles.Accounting.ACCUser, false)]
    [InlineData(ApplicationRoles.Warehouse.KHOUser, false)]
    [InlineData(ApplicationRoles.Lab.LabUser, false)]
    [InlineData(ApplicationRoles.Sales.SaleUser, false)]
    [InlineData("", false)]
    public async Task ExportColumnsFollowStockScreenRoleVisibility(string role, bool allowed)
    {
        var sender = DispatchProxy.Create<ISender, Sender>();
        var renderer = new Renderer();
        var user = new User(true, Guid.NewGuid()) { Roles = [role] };
        var result = await new ExportStockAvailableQueryHandler(sender, user, renderer).Handle(new(new()), default);
        Assert.True(result.Success);
        Assert.Equal(allowed, renderer.Visibility!.InactiveShelfFields);
        using var stream = new MemoryStream(new StockAvailableExcelRenderer().Render(
            [new() { Code = "ITEM", TotalOnHandKg = 10, InactiveShelfOnHandKg = 90, PhysicalOnHandKg = 100,
                StockDetailAvailables = [new() { ShelfStockCode = "A", OnHandKg = 10, ShelfIsActive = true }] }], renderer.Visibility, default));
        using var workbook = new XLWorkbook(stream);
        var summary = workbook.Worksheet("Tong hop");
        var detail = workbook.Worksheet("Chi tiet ke lot");
        var headers = summary.Row(1).CellsUsed().Select(x => x.GetString()).ToArray();
        var detailHeaders = detail.Row(1).CellsUsed().Select(x => x.GetString()).ToArray();
        Assert.Equal(allowed, headers.Contains("Tồn kệ đã tắt kg"));
        Assert.Equal(allowed, detailHeaders.Contains("Trạng thái kệ"));
        Assert.DoesNotContain("Tổng tồn vật lý kg", headers);
        Assert.Equal(allowed ? 8 : 7, summary.LastColumnUsed()!.ColumnNumber());
        Assert.Equal(allowed ? 12 : 11, detail.LastColumnUsed()!.ColumnNumber());
        Assert.Contains("Khả dụng kg", headers);
    }
}
