using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Entities.MROSchema;

namespace HRM.Application.Tests.Features.PLM.Boms;

public sealed class ManufacturingProcessTemplateSnapshotFactoryTests
{
    [Fact]
    public void CreateStage_CopiesMachineParametersIntoIndependentSnapshots()
    {
        var parameter = new ManufacturingProcessTemplateStageMachineParameter
        {
            ManufacturingProcessTemplateStageMachineParameterId = Guid.NewGuid(),
            ParameterCode = "BARREL_TEMP",
            ParameterName = "Nhiệt độ nòng",
            TargetValue = 180m,
            MinValue = 175m,
            MaxValue = 185m,
            Unit = "°C",
            IsRequired = true,
            SequenceNo = 1,
            Note = "Ổn định trước khi đùn"
        };
        var source = new ManufacturingProcessTemplateStage
        {
            ManufacturingProcessTemplateStageId = Guid.NewGuid(),
            ExternalId = "EXTRUSION",
            Code = "EXTRUSION",
            Name = "Đùn",
            SequenceNo = 1,
            Machines =
            [
                new ManufacturingProcessTemplateStageMachine
                {
                    ManufacturingProcessTemplateStageMachineId = Guid.NewGuid(),
                    EquipmentId = 42,
                    Equipment = new EquipmentMRO { EquipmentId = 42, EquipmentExternalId = "EXT-01", EquipmentName = "Máy đùn 01" },
                    IsDefault = true,
                    SequenceNo = 1,
                    Parameters = [parameter]
                }
            ]
        };

        var snapshot = ManufacturingProcessTemplateSnapshotFactory.CreateStage(Guid.NewGuid(), source, DateTime.Now);
        var copied = Assert.Single(Assert.Single(snapshot.Machines).Parameters);

        Assert.NotEqual(parameter.ManufacturingProcessTemplateStageMachineParameterId, copied.ManufacturingBomStageMachineParameterId);
        Assert.Equal("BARREL_TEMP", copied.ParameterCodeSnapshot);
        Assert.Equal(180m, copied.TargetValueSnapshot);
        Assert.Equal(175m, copied.MinValueSnapshot);
        Assert.Equal(185m, copied.MaxValueSnapshot);
        Assert.Equal("°C", copied.UnitSnapshot);
        Assert.True(copied.IsRequiredSnapshot);

        parameter.TargetValue = 200m;
        parameter.Note = "Template changed";
        Assert.Equal(180m, copied.TargetValueSnapshot);
        Assert.Equal("Ổn định trước khi đùn", copied.NoteSnapshot);
    }

    [Fact]
    public void CreateTransition_CopiesRoutingValuesAndTargetsClonedStages()
    {
        var source = new ManufacturingProcessTemplateStageTransition
        {
            ManufacturingProcessTemplateStageTransitionId = Guid.NewGuid(),
            ExternalId = "MPTT-001",
            Code = "MIX-EXTRUSION",
            TransitionType = HRM.Domain.Enums.Boms.ManufacturingStageTransitionType.ProcessFlow,
            DefaultEventCount = 2,
            SequenceNo = 1,
            Note = "Chuyển bán thành phẩm"
        };
        var bomVersionId = Guid.NewGuid();
        var fromStageId = Guid.NewGuid();
        var toStageId = Guid.NewGuid();

        var snapshot = ManufacturingProcessTemplateSnapshotFactory.CreateTransition(bomVersionId, source, fromStageId, toStageId);

        Assert.Equal(bomVersionId, snapshot.BomVersionId);
        Assert.Equal(fromStageId, snapshot.FromManufacturingBomStageId);
        Assert.Equal(toStageId, snapshot.ToManufacturingBomStageId);
        Assert.Equal("MIX-EXTRUSION", snapshot.ExternalId);
        Assert.Equal(2, snapshot.DefaultEventCount);
        Assert.NotEqual(source.ManufacturingProcessTemplateStageTransitionId, snapshot.ManufacturingBomStageTransitionId);
    }
}
