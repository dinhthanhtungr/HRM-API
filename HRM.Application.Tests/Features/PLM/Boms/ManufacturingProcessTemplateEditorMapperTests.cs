using HRM.Application.Features.PLM.Boms.Mappers;
using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Entities.MROSchema;

namespace HRM.Application.Tests.Features.PLM.Boms;

public sealed class ManufacturingProcessTemplateEditorMapperTests
{
    [Fact]
    public void ToEditorDto_GroupsMachinesAndOmitsPhysicalParameterIdFromSharedConfiguration()
    {
        var groupKey = Guid.NewGuid();
        var template = CreateTemplate(groupKey, secondTargetValue: 30);

        var result = ManufacturingTemplateMapper.ToEditorDto(template);

        var group = Assert.Single(Assert.Single(result.Stages).MachineConfigurationGroups);
        Assert.Equal(groupKey, group.ConfigurationGroupKey);
        Assert.True(group.IsConfigurationConsistent);
        Assert.Equal(2, group.Machines.Count);
        var parameter = Assert.Single(group.Parameters);
        Assert.Equal("MIX_TIME", parameter.ParameterCode);
        Assert.Equal(30, parameter.TargetValue);
    }

    [Fact]
    public void ToEditorDto_FlagsInconsistentPersistedGroupConfiguration()
    {
        var template = CreateTemplate(Guid.NewGuid(), secondTargetValue: 45);

        var result = ManufacturingTemplateMapper.ToEditorDto(template);

        Assert.False(Assert.Single(Assert.Single(result.Stages).MachineConfigurationGroups).IsConfigurationConsistent);
    }

    private static ManufacturingProcessTemplate CreateTemplate(Guid groupKey, decimal secondTargetValue)
    {
        var stage = new ManufacturingProcessTemplateStage
        {
            ManufacturingProcessTemplateStageId = Guid.NewGuid(),
            ExternalId = "MPS-001",
            Code = "MIX",
            Name = "Trộn",
            SequenceNo = 1
        };
        stage.Machines =
        [
            CreateMachine(stage.ManufacturingProcessTemplateStageId, 11, 1, groupKey, 30),
            CreateMachine(stage.ManufacturingProcessTemplateStageId, 12, 2, groupKey, secondTargetValue)
        ];

        return new ManufacturingProcessTemplate
        {
            ManufacturingProcessTemplateId = Guid.NewGuid(),
            ExternalId = "MPT-001",
            Name = "Quy trình trộn",
            VersionNo = 1,
            Stages = [stage]
        };
    }

    private static ManufacturingProcessTemplateStageMachine CreateMachine(
        Guid stageId,
        int equipmentId,
        int sequenceNo,
        Guid groupKey,
        decimal targetValue) => new()
    {
        ManufacturingProcessTemplateStageMachineId = Guid.NewGuid(),
        ManufacturingProcessTemplateStageId = stageId,
        EquipmentId = equipmentId,
        Equipment = new EquipmentMRO
        {
            EquipmentId = equipmentId,
            EquipmentExternalId = $"MIX-{equipmentId}",
            EquipmentName = $"Máy trộn {equipmentId}",
            AreaExternalId = "AREA-01",
            FactoryExternalId = "FACTORY-01"
        },
        ConfigurationGroupKey = groupKey,
        ConfigurationGroupName = "Máy trộn chuẩn",
        Note = "Không mở nắp khi máy chạy",
        SequenceNo = sequenceNo,
        Parameters =
        [
            new ManufacturingProcessTemplateStageMachineParameter
            {
                ManufacturingProcessTemplateStageMachineParameterId = Guid.NewGuid(),
                ParameterCode = "MIX_TIME",
                ParameterName = "Thời gian trộn",
                TargetValue = targetValue,
                Unit = "minute",
                IsRequired = true,
                SequenceNo = 1
            }
        ]
    };
}
