using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Products;

namespace HRM.Application.Tests.Features.PLM.Formulas;

public sealed class FormulaMaterialMutationRulesTests
{
    [Theory]
    [InlineData(FormulaStatus.SampleSent)]
    [InlineData(FormulaStatus.Completed)]
    [InlineData(FormulaStatus.Cancelled)]
    public void EnsureCanReplaceMaterials_RejectsCompositionChangeForLockedStatus(FormulaStatus status)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            FormulaMaterialMutationRules.EnsureCanReplaceMaterials(
                status.ToString(),
                hasCompositionChanges: true));

        Assert.Contains(status.ToString(), exception.Message);
    }

    [Theory]
    [InlineData(FormulaStatus.Draft)]
    [InlineData(FormulaStatus.Approved)]
    [InlineData(FormulaStatus.PendingSaleConfirmation)]
    [InlineData(FormulaStatus.Rejected)]
    [InlineData(FormulaStatus.Unknown)]
    public void EnsureCanReplaceMaterials_AllowsCompositionChangeForOtherStatuses(FormulaStatus status)
    {
        FormulaMaterialMutationRules.EnsureCanReplaceMaterials(
            status.ToString(),
            hasCompositionChanges: true);
    }

    [Theory]
    [InlineData(FormulaStatus.SampleSent)]
    [InlineData(FormulaStatus.Completed)]
    [InlineData(FormulaStatus.Cancelled)]
    public void EnsureCanReplaceMaterials_AllowsUnchangedCompositionForLockedStatus(FormulaStatus status)
    {
        FormulaMaterialMutationRules.EnsureCanReplaceMaterials(
            status.ToString(),
            hasCompositionChanges: false);
    }

    [Fact]
    public void HasCompositionChanges_ReturnsFalseForEquivalentResubmittedMaterials()
    {
        var itemId = Guid.NewGuid();
        FormulaMaterialCompositionItem[] current =
        [
            new(ItemType.Material, itemId, 1.25m)
        ];
        FormulaMaterialCompositionItem[] requested =
        [
            new(ItemType.Material, itemId, 1.25m)
        ];

        Assert.False(FormulaMaterialMutationRules.HasCompositionChanges(current, requested));
    }

    [Fact]
    public void HasCompositionChanges_ReturnsTrueWhenQuantityChanges()
    {
        var itemId = Guid.NewGuid();
        FormulaMaterialCompositionItem[] current =
        [
            new(ItemType.Material, itemId, 1.25m)
        ];
        FormulaMaterialCompositionItem[] requested =
        [
            new(ItemType.Material, itemId, 1.5m)
        ];

        Assert.True(FormulaMaterialMutationRules.HasCompositionChanges(current, requested));
    }
}
