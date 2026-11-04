using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;

using Xunit;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingNoteRulesTests
{
    [Fact]
    public void HeaderOnlyEdit_PreservesStoredAutoWithoutAcceptingClientGeneratedText()
    {
        var stored = "[[AUTO_PLPU_NOTE]]\nNVL: Thêm: A\n[[/AUTO_PLPU_NOTE]]\nold manual";
        var supplied = "[[AUTO_PLPU_NOTE]]\nforged\n[[/AUTO_PLPU_NOTE]]\nnew manual";
        var note = ManufacturingNoteRules.PreserveStoredPlpuAuto(supplied, stored);
        Assert.Contains("NVL: Thêm: A", note);
        Assert.DoesNotContain("forged", note);
        Assert.DoesNotContain("old manual", note);
        Assert.EndsWith("new manual", note);
    }
    [Fact]
    public void PlpuNote_ReplacesSuppliedAutoBlockWithServerChangesAndKeepsManualText()
    {
        var supplied = "before\n[[AUTO_PLPU_NOTE]]\nforged\n[[/AUTO_PLPU_NOTE]]\nafter";
        var result = ManufacturingNoteRules.ComposePlpu(supplied, ["A => B", "A => B"], ["C"], ["D"]);
        Assert.DoesNotContain("forged", result);
        Assert.Contains("NVL: Thay đổi: A => B | Thêm: C | Xóa: D", result);
        Assert.EndsWith($"before{Environment.NewLine}{Environment.NewLine}after", result);
    }

    [Fact]
    public void QuantityOnlyChanges_DoNotGenerateMaterialCodeNote()
        => Assert.Equal("manual", ManufacturingNoteRules.ComposePlpu(" manual ", [], [], []));

    [Fact]
    public void MaterialChanges_StampServerDateAndKeepItOnManualOnlyEdits()
    {
        var date = new DateTime(2026, 10, 28, 9, 35, 0);
        var saved = ManufacturingNoteRules.ComposePlpu("Manual note", ["A => B"], [], [], date);
        Assert.Contains("Ngày chỉnh: 28/10/2026 09:35", saved);
        Assert.EndsWith("Manual note", saved);
        var updated = ManufacturingNoteRules.PreserveStoredPlpuAuto("New manual note", saved);
        Assert.Contains("Ngày chỉnh: 28/10/2026 09:35", updated);
        Assert.EndsWith("New manual note", updated);
        Assert.Equal("Manual note", ManufacturingNoteRules.ComposePlpu("Manual note", [], [], [], date));
    }

    [Fact]
    public async Task Resolver_UsesInjectedServerClockForActualCodeChanges()
    {
        var f = new ManufacturingFormulaCreationTests.Fixture(HRM.Domain.Enums.Manufacturings.ManufacturingFormulaOrigin.Production);
        var original = f.Db.ManufacturingFormulaMaterials.Single();
        var replacementId = Guid.NewGuid();
        var resolver = new ManufacturingFormulaInputResolver(f.Db, null!, new ManufacturingFormulaCreationTests.Clock(), new HRM.Application.Commons.Authorization.CurrentUserPermissionService(f.User));
        var note = await resolver.BuildPlpuNoteAsync(f.Order,
            new(f.SourceId, "SOURCE-VA", HRM.Domain.Enums.Manufacturings.ManufacturingFormulaOrigin.Production,
                null, ManufacturingFormulaCreationTests.Fixture.Now, true),
            [new() { SourceItemId = original.ManufacturingFormulaMaterialId, ItemId = replacementId,
                ItemType = HRM.Domain.Enums.Formulas.ItemType.Material, Quantity = 1 }],
            [new() { ItemId = replacementId, ItemType = HRM.Domain.Enums.Formulas.ItemType.Material,
                MaterialExternalIdSnapshot = "NEW-MATERIAL" }], "Giữ ghi chú nhập tay", default);
        Assert.Contains("Ngày chỉnh: 10/10/2026 12:00", note);
        Assert.Contains("CURRENT-MATERIAL => NEW-MATERIAL", note);
        Assert.EndsWith("Giữ ghi chú nhập tay", note);
    }

    [Theory]
    [InlineData(null, null, "")]
    [InlineData(" user note ", null, "user note")]
    [InlineData(null, " source note ", "Lab lưu ý: source note")]
    public void LabNote_TrimsAndPreservesLegacyPrefix(string? manual, string? formula, string expected)
        => Assert.Equal(expected, ManufacturingNoteRules.MergeLab(manual, formula));
}
