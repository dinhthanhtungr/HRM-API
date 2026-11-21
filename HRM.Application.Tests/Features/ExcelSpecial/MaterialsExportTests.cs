using ClosedXML.Excel;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.ExcelSpecial.Queries.ExportMaterials;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Infrastructure.Documents.Excels;

namespace HRM.Application.Tests.Features.ExcelSpecial;

public sealed class MaterialsExportTests
{
    [Theory]
    [InlineData(ApplicationPermissions.Purchasing.ManageCatalog)]
    [InlineData(ApplicationPermissions.Purchasing.ManagePurchaseOrders)]
    public async Task Export_UsesMaterialCategoryAndRestrictsCompanyActiveAndType(string permission)
    {
        var user = new IqcWorkflowTests.User { HasExplicitPermissionSet = true, Permissions = [permission] };
        var company = user.CompanyId!.Value;
        var other = Guid.NewGuid();
        var category = new Category { CategoryId = Guid.NewGuid(), CompanyId = company, Types = "NVL", ExternalId = "PIG", Name = "Bột màu", IsActive = true };
        var foreign = new Category { CategoryId = Guid.NewGuid(), CompanyId = other, Types = "NVL", IsActive = true };
        var inactive = new Category { CategoryId = Guid.NewGuid(), CompanyId = company, Types = "NVL", IsActive = false };
        var product = new Category { CategoryId = Guid.NewGuid(), CompanyId = company, Types = "TP", IsActive = true };
        var (db, store) = IqcTestStore.CreateContext<IPLMReadDbContext>();
        store.Seed(nameof(db.Categories), category, foreign, inactive, product);
        Material Row(string code, Category group, Guid? owner = null, bool active = true) => new()
        {
            MaterialId = Guid.NewGuid(), ExternalId = code, Name = "NVL " + code, CustomCode = "VU-" + code,
            CompanyId = owner ?? company, CategoryId = group.CategoryId, IsActive = active
        };
        store.Seed(nameof(db.Materials), Row("B", category), Row("A", category),
            Row("OTHER", category, other), Row("BAD-CATEGORY", foreign),
            Row("INACTIVE", category, active: false), Row("INACTIVE-CATEGORY", inactive), Row("PRODUCT", product));
        var renderer = new CaptureRenderer();
        var result = await new ExportMaterialsQueryHandler(db, user, new CurrentUserPermissionService(user), renderer)
            .Handle(new(), default);

        Assert.True(result.Success);
        Assert.Equal(new[] { "A", "B" }, renderer.Rows!.Select(x => x.MaterialCode));
        Assert.All(renderer.Rows!, row =>
        {
            Assert.Equal("PIG", row.CategoryCode);
            Assert.Equal("Bột màu", row.CategoryName);
            Assert.Equal("NVL " + row.MaterialCode, row.MaterialName);
            Assert.Equal("VU-" + row.MaterialCode, row.VietausCode);
        });
    }

    [Theory]
    [InlineData("permission")]
    [InlineData("company")]
    [InlineData("unauthenticated")]
    public async Task Export_DeniesInvalidContextBeforeReadingData(string scenario)
    {
        var user = new IqcWorkflowTests.User
        {
            HasExplicitPermissionSet = true,
            Permissions = scenario == "permission" ? [] : [ApplicationPermissions.Purchasing.ManageCatalog],
            CompanyId = scenario == "company" ? null : Guid.NewGuid(),
            IsAuthenticated = scenario != "unauthenticated"
        };
        var renderer = new CaptureRenderer();
        var handler = new ExportMaterialsQueryHandler(null!, user, new CurrentUserPermissionService(user), renderer);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new(), default));
        Assert.Null(renderer.Rows);
    }

    [Fact]
    public void Workbook_ContainsFourColumnsTextValuesAndEmptyExport()
    {
        var renderer = new MaterialsExcelRenderer();
        using var stream = new MemoryStream(renderer.Render([new("001", "=1+1", "PIG", "Bột màu")]));
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);
        Assert.Equal("Mã NVL", sheet.Cell(1, 1).GetString());
        Assert.Equal("Tên loại sản phẩm", sheet.Cell(1, 4).GetString());
        Assert.Equal("001", sheet.Cell(2, 1).GetString());
        Assert.Equal("=1+1", sheet.Cell(2, 2).GetString());
        Assert.False(sheet.Cell(2, 2).HasFormula);
        Assert.Equal("PIG", sheet.Cell(2, 3).GetString());
        Assert.Equal("Bột màu", sheet.Cell(2, 4).GetString());
        using var emptyStream = new MemoryStream(renderer.Render([]));
        using var emptyBook = new XLWorkbook(emptyStream);
        Assert.Equal(1, emptyBook.Worksheet(1).LastRowUsed()!.RowNumber());
    }

    [Theory]
    [InlineData("Bột màu trắng", " ABC123 ", "Bột màu trắng (ABC123)")]
    [InlineData("Bột màu trắng", null, "Bột màu trắng")]
    [InlineData("Bột màu trắng", "   ", "Bột màu trắng")]
    [InlineData(null, "ABC123", "(ABC123)")]
    [InlineData("=1+1", "ABC123", "=1+1 (ABC123)")]
    public void Workbook_AppendsVietausCodeToName_WhenAvailable(string? name, string? code, string expected)
    {
        using var stream = new MemoryStream(new MaterialsExcelRenderer().Render([new("NVL", name, "PIG", "Bột màu", code)]));
        using var workbook = new XLWorkbook(stream);
        var cell = workbook.Worksheet(1).Cell(2, 2);
        Assert.Equal(expected, cell.GetString());
        Assert.False(cell.HasFormula);
        Assert.Equal(4, workbook.Worksheet(1).LastColumnUsed()!.ColumnNumber());
    }

    private sealed class CaptureRenderer : IMaterialsExcelRenderer
    {
        public IReadOnlyList<MaterialExcelRow>? Rows { get; private set; }
        public byte[] Render(IReadOnlyList<MaterialExcelRow> rows) { Rows = rows; return [1]; }
    }
}
