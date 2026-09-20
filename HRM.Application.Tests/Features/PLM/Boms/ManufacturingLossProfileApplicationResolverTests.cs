using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Enums.Boms;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Tests.Features.PLM.Boms;

public sealed class ManufacturingLossProfileApplicationResolverTests
{
    [Fact]
    public void Resolve_RejectsReleasedBom()
    {
        var version = CreateBomVersion(BomVersionStatus.Released);
        var profile = CreateProfile();

        var result = ManufacturingLossProfileApplicationResolver.Resolve(version, profile, DateTime.Now);

        Assert.Equal("Only Draft Manufacturing BOM versions can receive a loss profile.", result.Error);
    }

    [Fact]
    public void Resolve_RejectsProfileOutsideEffectivePeriod()
    {
        var version = CreateBomVersion(BomVersionStatus.Draft);
        var profile = CreateProfile();
        profile.EffectiveTo = new DateTime(2026, 9, 15);

        var result = ManufacturingLossProfileApplicationResolver.Resolve(
            version, profile, new DateTime(2026, 9, 16));

        Assert.Equal("Only a Released and currently effective loss profile can be applied.", result.Error);
    }

    [Fact]
    public void Resolve_MapsStageTransitionAndMaterialIntoIndependentBomRules()
    {
        var version = CreateBomVersion(BomVersionStatus.Draft);
        var mix = CreateStage(version, "MIX", 1);
        var pack = CreateStage(version, "PACK", 2);
        version.ManufacturingStages = [mix, pack];

        var transition = new ManufacturingBomStageTransition
        {
            ManufacturingBomStageTransitionId = Guid.NewGuid(),
            BomVersionId = version.BomVersionId,
            FromManufacturingBomStageId = mix.ManufacturingBomStageId,
            ToManufacturingBomStageId = pack.ManufacturingBomStageId,
            Code = "MIX-PACK",
            SequenceNo = 1
        };
        version.ManufacturingStageTransitions = [transition];

        var materialId = Guid.NewGuid();
        var item = new BomVersionItem
        {
            BomVersionItemId = Guid.NewGuid(),
            BomVersionId = version.BomVersionId,
            LineNo = 1,
            ItemType = ItemType.Material,
            MaterialId = materialId,
            Quantity = 1,
            Unit = "kg"
        };
        version.Items = [item];

        var profile = CreateProfile();
        var lossType = new ManufacturingLossType
        {
            ManufacturingLossTypeId = Guid.NewGuid(),
            Code = "PROCESS",
            Name = "Process loss"
        };
        profile.Rules =
        [
            CreateRule(profile, lossType, 1, ManufacturingLossScope.Stage, targetStageCode: "mix"),
            CreateRule(profile, lossType, 2, ManufacturingLossScope.Transition, fromStageCode: "MIX", toStageCode: "pack"),
            CreateRule(profile, lossType, 3, ManufacturingLossScope.Material, materialId: materialId)
        ];

        var result = ManufacturingLossProfileApplicationResolver.Resolve(
            version, profile, new DateTime(2026, 9, 16));

        Assert.Null(result.Error);
        Assert.Equal(3, result.Rules.Count);
        Assert.Equal(mix.ManufacturingBomStageId, result.Rules[0].ManufacturingBomStageId);
        Assert.Equal(transition.ManufacturingBomStageTransitionId, result.Rules[1].ManufacturingBomStageTransitionId);
        Assert.Equal(item.BomVersionItemId, result.Rules[2].BomVersionItemId);
        Assert.All(result.Rules, rule => Assert.Equal(version.BomVersionId, rule.BomVersionId));
        Assert.All(result.Rules, rule => Assert.NotEqual(Guid.Empty, rule.ManufacturingBomLossRuleId));
    }

    private static BomVersion CreateBomVersion(BomVersionStatus status)
        => new()
        {
            BomVersionId = Guid.NewGuid(),
            Status = status,
            BaseOutputQuantity = 1,
            OutputUnit = "kg"
        };

    private static ManufacturingLossProfile CreateProfile()
        => new()
        {
            ManufacturingLossProfileId = Guid.NewGuid(),
            Code = "DEFAULT",
            Name = "Default",
            Status = ManufacturingLossProfileStatus.Released,
            EffectiveFrom = new DateTime(2026, 1, 1)
        };

    private static ManufacturingBomStage CreateStage(BomVersion version, string code, int sequence)
        => new()
        {
            ManufacturingBomStageId = Guid.NewGuid(),
            BomVersionId = version.BomVersionId,
            Code = code,
            Name = code,
            SequenceNo = sequence,
            IsActive = true
        };

    private static ManufacturingLossProfileRule CreateRule(
        ManufacturingLossProfile profile,
        ManufacturingLossType lossType,
        int sequence,
        ManufacturingLossScope scope,
        Guid? materialId = null,
        string? targetStageCode = null,
        string? fromStageCode = null,
        string? toStageCode = null)
        => new()
        {
            ManufacturingLossProfileRuleId = Guid.NewGuid(),
            ManufacturingLossProfileId = profile.ManufacturingLossProfileId,
            ManufacturingLossTypeId = lossType.ManufacturingLossTypeId,
            LossType = lossType,
            Scope = scope,
            MaterialId = materialId,
            TargetStageCode = targetStageCode,
            FromStageCode = fromStageCode,
            ToStageCode = toStageCode,
            AllocationMethod = ManufacturingLossAllocationMethod.None,
            CalculationMethod = LossCalculationMethod.ActualOnly,
            SequenceNo = sequence,
            IsActive = true
        };
}
