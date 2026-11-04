using HRM.Application.Features.PLM.Manufacturing.Services.ColorChips;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.ColorChipRecords.Dtos;
using HRM.Application.Features.PLM.Manufacturing.Dtos.ColorChipManufacturingRecords;

using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.SampleRequests;
using Xunit;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingColorChipWorkflowTests
{
    [Fact]
    public async Task Create_ResolvesCurrentFormulaAndProductSnapshotsOnServer()
    {
        var f = new Fixture();
        var result = await f.Service.SaveAsync(null, new() { MfgProductionOrderId = f.Base.Order.MfgProductionOrderId }, default);
        Assert.True(result.Success, result.Message);
        var row = Assert.Single(f.Base.Store.Added.OfType<ColorChipManufacturingRecord>());
        Assert.Equal(f.Formula.ManufacturingFormulaId, row.ManufacturingFormulaId);
        Assert.Equal("MFG-COLOR", row.ColorCode);
        Assert.Equal("saved product", row.ProductName);
        Assert.Equal(ResinType.PP, row.ResinType);
        Assert.Equal(LogoType.Vietaus, row.LogoType);
        Assert.Equal(FormStyle.Chips2, row.FormStyle);
        Assert.Equal(ManufacturingFormulaCreationTests.Fixture.Now, row.RecordDate);
        Assert.Equal(f.Base.User.EmployeeId, row.CreatedBy);
        Assert.True(f.Base.Store.Transaction.Committed);
    }

    [Fact]
    public async Task Patch_NullClearsFullFormFieldsButKeepsEnumValues()
    {
        var f = new Fixture();
        var row = f.Existing();
        var result = await f.Service.SaveAsync(row.ColorChipMfgRecordId, new(), default);
        Assert.True(result.Success, result.Message);
        Assert.Null(row.MfgProductionOrderId);
        Assert.Null(row.ManufacturingFormulaId);
        Assert.Null(row.ColorCode);
        Assert.Null(row.UsageRate);
        Assert.Null(row.RecordDate);
        Assert.Null(row.Note);
        Assert.Equal(FormStyle.Chips2_NonStandard, row.FormStyle);
        Assert.Equal(ManufacturingFormulaCreationTests.Fixture.Now, row.UpdatedDate);
    }

    [Theory]
    [InlineData("company")]
    [InlineData("inactive")]
    [InlineData("formula-company")]
    [InlineData("unrelated")]
    [InlineData("capability")]
    [InlineData("nan")]
    public async Task InvalidScopeOrInput_DoesNotSave(string scenario)
    {
        var f = new Fixture();
        var formulaId = f.Formula.ManufacturingFormulaId;
        if (scenario == "company") f.Base.Order.CompanyId = Guid.NewGuid();
        if (scenario == "inactive") f.Base.Order.IsActive = false;
        if (scenario == "formula-company") f.Formula.CompanyId = Guid.NewGuid();
        if (scenario == "unrelated") formulaId = Guid.NewGuid();
        if (scenario == "capability") f.Base.User.Roles = [ApplicationRoles.Sales.SaleUser];
        var result = await f.Service.SaveAsync(null, new() { MfgProductionOrderId = f.Base.Order.MfgProductionOrderId,
            ManufacturingFormulaId = formulaId, UsageRate = scenario == "nan" ? double.NaN : null }, default);
        Assert.False(result.Success);
        Assert.Empty(f.Base.Store.Added);
        Assert.Equal(0, f.Base.Store.Saves);
    }

    [Fact]
    public async Task Create_PreservesManualTechnicalLabelsAndRollsBackWriteFailure()
    {
        var f = new Fixture();
        var request = new ManufacturingColorChipInput { ColorCode = "manual", ProductName = "manual product" };
        var result = await f.Service.SaveAsync(null, request, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal("manual", result.Data!.ColorCode);
        Assert.Equal("manual product", result.Data.ProductName);
        var failed = new Fixture();
        failed.Base.Store.ThrowOnSave = true;
        Assert.False((await failed.Service.SaveAsync(null, request, default)).Success);
        Assert.True(failed.Base.Store.Transaction.RolledBack);
        Assert.False(failed.Base.Store.Transaction.Committed);
    }

    [Fact]
    public async Task Pdf_UsesSavedLabelsAndRejectsEmptyRendererOutput()
    {
        var f = new Fixture();
        var row = f.Existing();
        Assert.True((await f.Service.PrintAsync(row.ColorChipMfgRecordId, default)).Success);
        Assert.Equal("manual color", f.Renderer.Model!.Code);
        Assert.Equal("manual product", f.Renderer.Model.Color);
        Assert.Equal(FormStyle.Chips2_NonStandard, f.Renderer.Style);
        f.Renderer.Bytes = [];
        Assert.False((await f.Service.PrintAsync(row.ColorChipMfgRecordId, default)).Success);
        row.CompanyId = Guid.NewGuid();
        f.Renderer.Model = null;
        Assert.False((await f.Service.PrintAsync(row.ColorChipMfgRecordId, default)).Success);
        Assert.Null(f.Renderer.Model);
    }

    private sealed class Fixture
    {
        public ManufacturingFormulaCreationTests.Fixture Base { get; } = new(ManufacturingFormulaOrigin.FromVu);
        public ManufacturingFormula Formula { get; }
        public Renderer Renderer { get; } = new();
        public ManufacturingColorChipService Service { get; }
        public Fixture()
        {
            Base.User.Roles = [ApplicationRoles.Lab.LabUser];
            Base.Order.ProductExternalIdSnapshot = "MFG-COLOR";
            Base.Order.ProductNameSnapshot = "saved product";
            Formula = Base.Db.ManufacturingFormulas.Single();
            Base.Store.Seed(nameof(Base.Db.MfgProductionOrders), Base.Order);
            Base.Store.Seed(nameof(Base.Db.ProductionSelectVersions), new ProductionSelectVersion {
                CompanyId = Base.Order.CompanyId, MfgProductionOrderId = Base.Order.MfgProductionOrderId,
                ManufacturingFormulaId = Formula.ManufacturingFormulaId, ValidFrom = ManufacturingFormulaCreationTests.Fixture.Now });
            Service = new(Base.Db, Base.User, new CurrentUserPermissionService(Base.User), new Clock(), Renderer);
        }
        public ColorChipManufacturingRecord Existing()
        {
            var row = new ColorChipManufacturingRecord { ColorChipMfgRecordId = Guid.NewGuid(), CompanyId = Base.Order.CompanyId,
                IsActive = true, MfgProductionOrderId = Base.Order.MfgProductionOrderId, MfgProductionOrder = Base.Order,
                ManufacturingFormulaId = Formula.ManufacturingFormulaId, ManufacturingFormula = Formula,
                ColorCode = "manual color", ProductName = "manual product", UsageRate = 2, Note = "saved",
                RecordDate = ManufacturingFormulaCreationTests.Fixture.Now.AddDays(-1), FormStyle = FormStyle.Chips2_NonStandard };
            Base.Store.Seed(nameof(Base.Db.ColorChipManufacturingRecords), row);
            return row;
        }
    }
    private sealed class Clock : IDateTimeProvider { public DateTime Now => ManufacturingFormulaCreationTests.Fixture.Now; }
    private sealed class Renderer : IColorChipRecordPdfRenderer
    {
        public ColorChipRecordPdfModel? Model;
        public FormStyle Style;
        public byte[] Bytes = [37, 80, 68, 70];
        public byte[] Render(ColorChipRecordPdfModel model, FormStyle formStyle) { Model = model; Style = formStyle; return Bytes; }
    }
}
