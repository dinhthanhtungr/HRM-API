using HRM.Application.Features.PLM.Boms.Rules;
using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Enums.Boms;

namespace HRM.Application.Tests.Features.PLM.Boms;

public sealed class ManufacturingProcessTemplateReleaseValidatorTests
{
    [Fact]
    public void Validate_ReturnsPathsForDefaultMachineAndParameterRangeErrors()
    {
        var template = CreateTemplate();
        template.Stages.Single().Machines =
        [
            new ManufacturingProcessTemplateStageMachine
            {
                EquipmentId = 10,
                SequenceNo = 1,
                Parameters =
                [
                    new ManufacturingProcessTemplateStageMachineParameter
                    {
                        ParameterCode = "MIX_TIME", ParameterName = "Thời gian trộn", Unit = "minute",
                        TargetValue = 40, MinValue = 20, MaxValue = 30, SequenceNo = 1
                    }
                ]
            }
        ];

        var issues = ManufacturingProcessTemplateReleaseValidator.Validate(template, new DateTime(2026, 9, 28));

        Assert.Contains(issues, x => x.Code == "MACHINE_DEFAULT_REQUIRED" && x.Path == "stages[MIX].machines");
        Assert.Contains(issues, x => x.Code == "PARAMETER_TARGET_OUT_OF_RANGE" && x.Path.EndsWith(".targetValue"));
    }

    [Fact]
    public void Validate_AcceptsCompleteDraftTemplate()
    {
        var template = CreateTemplate();
        template.Stages.Single().Machines =
        [
            new ManufacturingProcessTemplateStageMachine
            {
                EquipmentId = 10,
                IsDefault = true,
                SequenceNo = 1,
                Parameters =
                [
                    new ManufacturingProcessTemplateStageMachineParameter
                    {
                        ParameterCode = "MIX_TIME", ParameterName = "Thời gian trộn", Unit = "minute",
                        TargetValue = 25, MinValue = 20, MaxValue = 30, SequenceNo = 1
                    }
                ]
            }
        ];

        var issues = ManufacturingProcessTemplateReleaseValidator.Validate(template, new DateTime(2026, 9, 28));

        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_AcceptsParameterDefinitionWithoutOperatingValues()
    {
        var template = CreateTemplate();
        template.Stages.Single().Machines =
        [
            new ManufacturingProcessTemplateStageMachine
            {
                EquipmentId = 10,
                IsDefault = true,
                SequenceNo = 1,
                Parameters =
                [
                    new ManufacturingProcessTemplateStageMachineParameter
                    {
                        ParameterCode = "MIX_TIME",
                        ParameterName = "Thời gian trộn",
                        Unit = "minute",
                        IsRequired = true,
                        SequenceNo = 1
                    }
                ]
            }
        ];

        var issues = ManufacturingProcessTemplateReleaseValidator.Validate(
            template,
            new DateTime(2026, 9, 28));

        Assert.DoesNotContain(issues, x => x.Code == "PARAMETER_VALUE_REQUIRED");
        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_AcceptsOwnedDraftWorkInstructionForCascadeRelease()
    {
        var template = CreateTemplate();
        template.Stages.Single().WorkInstructionTemplate!.Status = ManufacturingTemplateStatus.Draft;

        var issues = ManufacturingProcessTemplateReleaseValidator.Validate(template, new DateTime(2026, 9, 28));

        Assert.DoesNotContain(issues, x =>
            x.Code is "WORK_INSTRUCTION_NOT_EFFECTIVE" or "WORK_INSTRUCTION_DRAFT_SHARED");
    }

    [Fact]
    public void Validate_RejectsDraftWorkInstructionSharedWithAnotherTemplate()
    {
        var template = CreateTemplate();
        var instruction = template.Stages.Single().WorkInstructionTemplate!;
        instruction.Status = ManufacturingTemplateStatus.Draft;
        var sharedIds = new HashSet<Guid> { instruction.ManufacturingWorkInstructionTemplateId };

        var issues = ManufacturingProcessTemplateReleaseValidator.Validate(
            template,
            new DateTime(2026, 9, 28),
            sharedIds);

        Assert.Contains(issues, x =>
            x.Code == "WORK_INSTRUCTION_DRAFT_SHARED" &&
            x.Path == "stages[MIX].manufacturingWorkInstructionTemplateId");
    }

    [Fact]
    public void Validate_RejectsObsoleteWorkInstruction()
    {
        var template = CreateTemplate();
        template.Stages.Single().WorkInstructionTemplate!.Status = ManufacturingTemplateStatus.Obsolete;

        var issues = ManufacturingProcessTemplateReleaseValidator.Validate(template, new DateTime(2026, 9, 28));

        Assert.Contains(issues, x => x.Code == "WORK_INSTRUCTION_NOT_EFFECTIVE");
    }

    private static ManufacturingProcessTemplate CreateTemplate()
    {
        var instruction = new ManufacturingWorkInstructionTemplate
        {
            Status = ManufacturingTemplateStatus.Released,
            IsActive = true,
            Procedure = "Trộn theo hướng dẫn"
        };
        var templateId = Guid.NewGuid();
        return new ManufacturingProcessTemplate
        {
            ManufacturingProcessTemplateId = templateId,
            Status = ManufacturingTemplateStatus.Draft,
            Stages =
            [
                new ManufacturingProcessTemplateStage
                {
                    ManufacturingProcessTemplateId = templateId,
                    Code = "MIX",
                    Name = "Trộn",
                    SequenceNo = 1,
                    WorkInstructionTemplate = instruction
                }
            ]
        };
    }
}
