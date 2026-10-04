using HRM.Application.Features.PLM.Boms.Commands.UpsertManufacturingProcessTemplate;
using HRM.Application.Features.PLM.Boms.Dtos;

namespace HRM.Application.Tests.Features.PLM.Boms;

public sealed class ManufacturingProcessTemplateMachineGroupPayloadNormalizerTests
{
    [Fact]
    public void TryExpand_CopiesSharedConfigurationToEveryGroupMember()
    {
        var groupKey = Guid.NewGuid();
        var parameter = new ManufacturingProcessTemplateStageMachineParameterWriteDto
        {
            ParameterCode = "MIX_TIME",
            ParameterName = "Thời gian trộn",
            TargetValue = 30,
            Unit = "minute",
            IsRequired = true,
            SequenceNo = 1
        };
        var request = new UpsertManufacturingProcessTemplateRequest
        {
            Name = "Quy trình trộn",
            Stages =
            [
                new ManufacturingProcessTemplateStageWriteDto
                {
                    Code = "MIX",
                    Name = "Trộn",
                    SequenceNo = 1,
                    MachineConfigurationGroups =
                    [
                        new ManufacturingProcessTemplateStageMachineConfigurationGroupWriteDto
                        {
                            ConfigurationGroupKey = groupKey,
                            ConfigurationGroupName = "Máy trộn 75 lít",
                            Note = "Không mở nắp khi máy chạy",
                            Parameters = [parameter],
                            Machines =
                            [
                                new ManufacturingProcessTemplateStageMachineMemberWriteDto { EquipmentId = 11, IsDefault = true, SequenceNo = 1 },
                                new ManufacturingProcessTemplateStageMachineMemberWriteDto { EquipmentId = 12, SequenceNo = 2 }
                            ]
                        }
                    ]
                }
            ]
        };

        var success = ManufacturingProcessTemplateMachineGroupPayloadNormalizer.TryExpand(request, out var expanded, out var error);

        Assert.True(success, error);
        var machines = expanded.Stages.Single().Machines;
        Assert.Equal(2, machines.Count);
        Assert.All(machines, machine =>
        {
            Assert.Equal(groupKey, machine.ConfigurationGroupKey);
            Assert.Equal("Máy trộn 75 lít", machine.ConfigurationGroupName);
            Assert.Equal("Không mở nắp khi máy chạy", machine.Note);
            Assert.Same(parameter, machine.Parameters.Single());
        });
        Assert.Single(machines, machine => machine.IsDefault);
    }

    [Fact]
    public void TryExpand_RejectsDuplicateGroupKeysWithinStage()
    {
        var groupKey = Guid.NewGuid();
        var request = new UpsertManufacturingProcessTemplateRequest
        {
            Name = "Quy trình trộn",
            Stages =
            [
                new ManufacturingProcessTemplateStageWriteDto
                {
                    Code = "MIX",
                    Name = "Trộn",
                    SequenceNo = 1,
                    MachineConfigurationGroups =
                    [
                        CreateGroup(groupKey, 11, 1),
                        CreateGroup(groupKey, 12, 2)
                    ]
                }
            ]
        };

        var success = ManufacturingProcessTemplateMachineGroupPayloadNormalizer.TryExpand(request, out _, out var error);

        Assert.False(success);
        Assert.Contains("ConfigurationGroupKey must be unique", error);
    }

    [Fact]
    public void TryExpand_RejectsEmptyGroupKeyOnFlatMachine()
    {
        var request = new UpsertManufacturingProcessTemplateRequest
        {
            Name = "Quy trình trộn",
            Stages =
            [
                new ManufacturingProcessTemplateStageWriteDto
                {
                    Code = "MIX",
                    Name = "Trộn",
                    SequenceNo = 1,
                    Machines =
                    [
                        new ManufacturingProcessTemplateStageMachineWriteDto
                        {
                            EquipmentId = 11,
                            SequenceNo = 1,
                            IsDefault = true,
                            ConfigurationGroupKey = Guid.Empty,
                            ConfigurationGroupName = "Nhóm không hợp lệ"
                        }
                    ]
                }
            ]
        };

        var success = ManufacturingProcessTemplateMachineGroupPayloadNormalizer.TryExpand(request, out _, out var error);

        Assert.False(success);
        Assert.Contains("invalid machine configuration group", error);
    }

    [Fact]
    public void TryExpand_PreservesExpectedUpdatedDate()
    {
        var expectedUpdatedDate = new DateTime(2026, 10, 1, 8, 30, 0, DateTimeKind.Utc);
        var request = new UpsertManufacturingProcessTemplateRequest
        {
            Name = "Quy trình trộn",
            ExpectedUpdatedDate = expectedUpdatedDate,
            Stages =
            [
                new ManufacturingProcessTemplateStageWriteDto
                {
                    Code = "MIX",
                    Name = "Trộn",
                    SequenceNo = 1
                }
            ]
        };

        var success = ManufacturingProcessTemplateMachineGroupPayloadNormalizer.TryExpand(request, out var expanded, out var error);

        Assert.True(success, error);
        Assert.Equal(expectedUpdatedDate, expanded.ExpectedUpdatedDate);
    }

    private static ManufacturingProcessTemplateStageMachineConfigurationGroupWriteDto CreateGroup(
        Guid groupKey,
        int equipmentId,
        int sequenceNo) => new()
    {
        ConfigurationGroupKey = groupKey,
        ConfigurationGroupName = "Nhóm máy trộn",
        Machines =
        [
            new ManufacturingProcessTemplateStageMachineMemberWriteDto
            {
                EquipmentId = equipmentId,
                SequenceNo = sequenceNo
            }
        ]
    };
}
