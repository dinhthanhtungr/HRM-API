using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Enums.Boms;

namespace HRM.Application.Features.PLM.Boms.Rules;

internal static class ManufacturingProcessTemplateReleaseValidator
{
    internal static IReadOnlyList<ManufacturingProcessTemplateValidationIssueDto> Validate(
        ManufacturingProcessTemplate template,
        DateTime now,
        IReadOnlySet<Guid>? sharedDraftWorkInstructionIds = null)
    {
        var issues = new List<ManufacturingProcessTemplateValidationIssueDto>();
        if (template.Status != ManufacturingTemplateStatus.Draft)
            Add(issues, "TEMPLATE_NOT_DRAFT", "status", "Chỉ template Draft mới có thể Release.");
        if (template.Stages.Count == 0)
            Add(issues, "STAGE_REQUIRED", "stages", "Template phải có ít nhất một công đoạn.");

        if (template.EffectiveFrom.HasValue && template.EffectiveTo.HasValue && template.EffectiveTo < template.EffectiveFrom)
            Add(issues, "EFFECTIVE_RANGE_INVALID", "effectiveTo", "Ngày kết thúc hiệu lực không được trước ngày bắt đầu.");

        foreach (var rule in template.ApplicabilityRules)
        {
            var path = $"applicabilityRules[{rule.ManufacturingProcessTemplateApplicabilityId}]";
            if (!rule.CategoryId.HasValue && !rule.StepOfProduct.HasValue)
                Add(issues, "APPLICABILITY_CONDITION_REQUIRED", path, "Quy tắc áp dụng phải có loại sản phẩm hoặc tuyến sản xuất.");
            if (rule.Priority < 0)
                Add(issues, "APPLICABILITY_PRIORITY_INVALID", path + ".priority", "Độ ưu tiên không được âm.");
            if (rule.StepOfProduct.HasValue && !Enum.IsDefined(rule.StepOfProduct.Value))
                Add(issues, "APPLICABILITY_STEP_INVALID", path + ".stepOfProduct", "Tuyến sản xuất không hợp lệ.");
            if (rule.CategoryId.HasValue && (rule.Category is null || rule.Category.CompanyId != template.CompanyId || rule.Category.IsActive != true))
                Add(issues, "APPLICABILITY_CATEGORY_INVALID", path + ".categoryId", "Loại sản phẩm phải đang hoạt động và thuộc cùng công ty.");
        }

        if (template.ApplicabilityRules.GroupBy(x => new { x.CategoryId, x.StepOfProduct }).Any(x => x.Count() > 1))
            Add(issues, "APPLICABILITY_DUPLICATE", "applicabilityRules", "Không được trùng tổ hợp loại sản phẩm và tuyến sản xuất.");

        foreach (var stage in template.Stages.OrderBy(x => x.SequenceNo))
        {
            var stagePath = $"stages[{stage.Code}]";
            if (string.IsNullOrWhiteSpace(stage.Code))
                Add(issues, "STAGE_CODE_REQUIRED", stagePath + ".code", "Mã công đoạn là bắt buộc.");
            if (string.IsNullOrWhiteSpace(stage.Name))
                Add(issues, "STAGE_NAME_REQUIRED", stagePath + ".name", "Tên công đoạn là bắt buộc.");
            if (stage.Machines.Count > 0 && stage.Machines.Count(x => x.IsDefault) != 1)
                Add(issues, "MACHINE_DEFAULT_REQUIRED", stagePath + ".machines", "Công đoạn có máy phải có đúng một máy mặc định.");

            foreach (var machine in stage.Machines)
            {
                if (machine.ConfigurationGroupKey == Guid.Empty ||
                    machine.ConfigurationGroupKey.HasValue && string.IsNullOrWhiteSpace(machine.ConfigurationGroupName) ||
                    !machine.ConfigurationGroupKey.HasValue && !string.IsNullOrWhiteSpace(machine.ConfigurationGroupName))
                    Add(issues, "MACHINE_GROUP_INVALID", $"{stagePath}.machines[{machine.EquipmentId}]", "Nhóm cấu hình máy không hợp lệ.");
            }

            foreach (var group in stage.Machines.Where(x => x.ConfigurationGroupKey.HasValue).GroupBy(x => x.ConfigurationGroupKey!.Value))
            {
                var reference = group.First();
                if (group.Skip(1).Any(machine =>
                        !ManufacturingProcessTemplateMachineConfigurationRules.HasSameSharedConfiguration(reference, machine)))
                    Add(issues, "MACHINE_GROUP_CONFIGURATION_INCONSISTENT", $"{stagePath}.machineConfigurationGroups[{group.Key}]", "Các máy trong cùng nhóm phải có cùng tên nhóm, lưu ý và thông số vận hành.");
            }

            if (stage.WorkInstructionTemplate is { } instruction &&
                (!instruction.IsActive || instruction.Status == ManufacturingTemplateStatus.Obsolete ||
                 instruction.EffectiveFrom.HasValue && instruction.EffectiveFrom > now ||
                 instruction.EffectiveTo.HasValue && instruction.EffectiveTo < now))
                Add(issues, "WORK_INSTRUCTION_NOT_EFFECTIVE", stagePath + ".manufacturingWorkInstructionTemplateId", "Work Instruction phải active, chưa Obsolete và còn hiệu lực.");

            if (stage.WorkInstructionTemplate is { Status: ManufacturingTemplateStatus.Draft } draftInstruction &&
                sharedDraftWorkInstructionIds?.Contains(draftInstruction.ManufacturingWorkInstructionTemplateId) == true)
                Add(issues, "WORK_INSTRUCTION_DRAFT_SHARED", stagePath + ".manufacturingWorkInstructionTemplateId", "Work Instruction Draft đang được template khác sử dụng nên không thể tự động ban hành cùng template này.");

            foreach (var machine in stage.Machines)
            foreach (var parameter in machine.Parameters)
            {
                var parameterPath = $"{stagePath}.machines[{machine.EquipmentId}].parameters[{parameter.ParameterCode}]";
                if (parameter.MinValue.HasValue && parameter.MaxValue.HasValue && parameter.MinValue > parameter.MaxValue)
                    Add(issues, "PARAMETER_RANGE_INVALID", parameterPath + ".maxValue", "Max phải lớn hơn hoặc bằng Min.");
                if ((parameter.TargetValue.HasValue && parameter.MinValue.HasValue && parameter.TargetValue < parameter.MinValue) ||
                    (parameter.TargetValue.HasValue && parameter.MaxValue.HasValue && parameter.TargetValue > parameter.MaxValue))
                    Add(issues, "PARAMETER_TARGET_OUT_OF_RANGE", parameterPath + ".targetValue", "Target phải nằm trong khoảng Min và Max.");
            }
        }

        foreach (var transition in template.StageTransitions)
        {
            var path = $"stageTransitions[{transition.Code}]";
            if (transition.FromManufacturingProcessTemplateStageId == transition.ToManufacturingProcessTemplateStageId)
                Add(issues, "TRANSITION_SELF_REFERENCE", path, "Đường chuyển không được nối một công đoạn với chính nó.");
            if (transition.DefaultEventCount <= 0)
                Add(issues, "TRANSITION_EVENT_COUNT_INVALID", path + ".defaultEventCount", "Số lần mặc định phải lớn hơn 0 khi được khai báo.");
        }

        return issues;
    }

    private static void Add(List<ManufacturingProcessTemplateValidationIssueDto> issues, string code, string path, string message)
        => issues.Add(new ManufacturingProcessTemplateValidationIssueDto { Code = code, Path = path, Message = message });
}
