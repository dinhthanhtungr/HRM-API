using ClosedXML.Excel;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.ExcelSpecial.Queries.ExportMaterialUsage;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Infrastructure.Documents.Excels;

namespace HRM.Application.Tests.Features.ExcelSpecial;

public sealed class MaterialUsageExportTests
{
    [Fact]
    public void Selection_LatestMatchingVaPerColour_IncludesDeterministicTieBreak()
    {
        var old = new MaterialUsageCandidate(Guid.NewGuid(), "VA1", new(2026, 1, 1), "BLUE", "Old");
        var current = old with { ManufacturingFormulaId = Guid.NewGuid(), ExternalId = "VA2", CreatedDate = new(2026, 2, 1), ColourCode = "blue", Name = "New" };
        var sameDate = current with { ManufacturingFormulaId = Guid.NewGuid(), ExternalId = "VA3" };
        var green = old with { ColourCode = "GREEN" };
        var rows = MaterialUsageSelection.Latest([old, current, current, sameDate, green]);
        Assert.Equal(2, rows.Count);
        Assert.Equal("VA3", rows[0].ExternalId);
        Assert.Equal("GREEN", rows[1].ColourCode);
    }

    [Theory]
    [InlineData("President", true, true)]
    [InlineData("Developer", false, true)]
    [InlineData("Developer", true, false)]
    public async Task DeniesRoleCapabilityOrVisibilityBeforeReading(string role, bool permission, bool materials)
    {
        var user = new IqcWorkflowTests.User { Roles = [role], HasExplicitPermissionSet = true,
            Permissions = permission ? [ApplicationPermissions.PLM.ViewProductionOrders] : [] };
        var handler = new ExportMaterialUsageQueryHandler(null!, user, new CurrentUserPermissionService(user),
            new ManufacturingVaExportTests.Visibility(materials), new Capture());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new(["NVL_NH_009"]), default));
    }

    [Fact]
    public async Task RejectsEmptyInputBeforeReading()
    {
        var user = new IqcWorkflowTests.User { Roles = ["Developer"], HasExplicitPermissionSet = true,
            Permissions = [ApplicationPermissions.PLM.ViewProductionOrders] };
        var handler = new ExportMaterialUsageQueryHandler(null!, user, new CurrentUserPermissionService(user),
            new ManufacturingVaExportTests.Visibility(true), new Capture());
        Assert.False((await handler.Handle(new([]), default)).Success);
        Assert.False((await handler.Handle(new([new string('A', 101)]), default)).Success);
    }

    [Theory]
    [InlineData("2026-11-27", "2026-11-26")]
    [InlineData(null, "9999-12-31")]
    public async Task RejectsInvalidDateRangeBeforeReading(string? from, string? to)
    {
        var user = new IqcWorkflowTests.User { Roles = ["Developer"], HasExplicitPermissionSet = true,
            Permissions = [ApplicationPermissions.PLM.ViewProductionOrders] };
        var handler = new ExportMaterialUsageQueryHandler(null!, user, new CurrentUserPermissionService(user),
            new ManufacturingVaExportTests.Visibility(true), new Capture());
        Assert.False((await handler.Handle(new(["NVL_NH_009"],
            from is null ? null : DateTime.Parse(from, System.Globalization.CultureInfo.InvariantCulture),
            to is null ? null : DateTime.Parse(to, System.Globalization.CultureInfo.InvariantCulture)), default)).Success);
    }
    [Fact]
    public void Excel_UsesTextCellsSixHeadersAndRealDates()
    {
        using var stream = new MemoryStream(new MaterialUsageExcelRenderer().Render([
            new("001", "=1+1", "VA123", "NVL_A, NVL_B", new(2026, 11, 26))]));
        using var book = new XLWorkbook(stream);
        var sheet = book.Worksheet(1);
        Assert.Equal(6, sheet.LastColumnUsed()!.ColumnNumber());
        Assert.Equal("STT", sheet.Cell(1, 1).GetString());
        Assert.Equal("Ngày VA mới nhất", sheet.Cell(1, 6).GetString());
        Assert.Equal("001", sheet.Cell(2, 2).GetString());
        Assert.Equal("=1+1", sheet.Cell(2, 3).GetString());
        Assert.False(sheet.Cell(2, 3).HasFormula);
        Assert.Equal(new DateTime(2026, 11, 26), sheet.Cell(2, 6).GetDateTime());
        using var empty = new MemoryStream(new MaterialUsageExcelRenderer().Render([]));
        using var emptyBook = new XLWorkbook(empty);
        Assert.Equal(1, emptyBook.Worksheet(1).LastRowUsed()!.RowNumber());
    }

    private sealed class Capture : IMaterialUsageExcelRenderer
    {
        public byte[] Render(IReadOnlyList<MaterialUsageExcelRow> rows) => [1];
    }
}