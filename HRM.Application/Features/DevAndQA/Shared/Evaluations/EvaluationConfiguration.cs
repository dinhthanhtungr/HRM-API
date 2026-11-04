using HRM.Application.Commons.Patching;
using HRM.Application.Features.DevAndQA.IQC.Services.EvaluationStandards;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Entities.DevandqaSchema;

namespace HRM.Application.Features.DevAndQA.Shared.Evaluations;

internal static class EvaluationConfiguration
{
    public static EvaluationConfigurationDto Read(EvaluationTemplateItem x) => new(x.DataType, x.Unit, x.MeasurementMethodId, x.MeasurementMethodCode, x.TestMethod, x.InputConfigJson, x.AcceptanceRuleJson, x.SpecificationText, x.IsRequired, x.SortOrder);

    public static bool Apply(EvaluationTemplateItem x, EvaluationConfigurationDto value)
    {
        var changed = false;
        changed |= PatchHelper.Set(value.DataType, () => x.DataType, v => x.DataType = v);
        changed |= PatchHelper.Set(value.Unit, () => x.Unit, v => x.Unit = v);
        changed |= PatchHelper.Set(value.MeasurementMethodId, () => x.MeasurementMethodId, v => x.MeasurementMethodId = v);
        changed |= PatchHelper.Set(value.MeasurementMethodCode, () => x.MeasurementMethodCode, v => x.MeasurementMethodCode = v);
        changed |= PatchHelper.Set(value.TestMethod, () => x.TestMethod, v => x.TestMethod = v);
        changed |= PatchHelper.Set(value.InputConfigJson, () => x.InputConfigJson, v => x.InputConfigJson = v);
        changed |= PatchHelper.Set(value.AcceptanceRuleJson, () => x.AcceptanceRuleJson, v => x.AcceptanceRuleJson = v);
        changed |= PatchHelper.Set(value.SpecificationText, () => x.SpecificationText, v => x.SpecificationText = v);
        changed |= PatchHelper.Set(value.IsRequired, () => x.IsRequired, v => x.IsRequired = v);
        changed |= PatchHelper.Set(value.SortOrder, () => x.SortOrder, v => x.SortOrder = v);
        return changed;
    }

    public static EvaluationConfigurationDto Read(IqcInspectionResult x) => new(x.DataType, x.Unit, x.MeasurementMethodId, x.MeasurementMethodCode, x.TestMethod, x.InputConfigJson, x.AcceptanceRuleJson, x.SpecificationText, x.IsRequired, x.SortOrder);

    public static bool Apply(IqcInspectionResult x, EvaluationConfigurationDto value)
    {
        var changed = false;
        changed |= PatchHelper.Set(value.DataType, () => x.DataType, v => x.DataType = v);
        changed |= PatchHelper.Set(value.Unit, () => x.Unit, v => x.Unit = v);
        changed |= PatchHelper.Set(value.MeasurementMethodId, () => x.MeasurementMethodId, v => x.MeasurementMethodId = v);
        changed |= PatchHelper.Set(value.MeasurementMethodCode, () => x.MeasurementMethodCode, v => x.MeasurementMethodCode = v);
        changed |= PatchHelper.Set(value.TestMethod, () => x.TestMethod, v => x.TestMethod = v);
        changed |= PatchHelper.Set(value.InputConfigJson, () => x.InputConfigJson, v => x.InputConfigJson = v);
        changed |= PatchHelper.Set(value.AcceptanceRuleJson, () => x.AcceptanceRuleJson, v => x.AcceptanceRuleJson = v);
        changed |= PatchHelper.Set(value.SpecificationText, () => x.SpecificationText, v => x.SpecificationText = v);
        changed |= PatchHelper.Set(value.IsRequired, () => x.IsRequired, v => x.IsRequired = v);
        changed |= PatchHelper.Set(value.SortOrder, () => x.SortOrder, v => x.SortOrder = v);
        return changed;
    }

    public static EvaluationConfigurationDto Read(CoaCertificateResult x) => new(x.DataType, x.Unit, x.MeasurementMethodId, x.MeasurementMethodCode, x.TestMethod, x.InputConfigJson, x.AcceptanceRuleJson, x.SpecificationText, x.IsRequired, x.SortOrder);

    public static bool Apply(CoaCertificateResult x, EvaluationConfigurationDto value)
    {
        var changed = false;
        changed |= PatchHelper.Set(value.DataType, () => x.DataType, v => x.DataType = v);
        changed |= PatchHelper.Set(value.Unit, () => x.Unit, v => x.Unit = v);
        changed |= PatchHelper.Set(value.MeasurementMethodId, () => x.MeasurementMethodId, v => x.MeasurementMethodId = v);
        changed |= PatchHelper.Set(value.MeasurementMethodCode, () => x.MeasurementMethodCode, v => x.MeasurementMethodCode = v);
        changed |= PatchHelper.Set(value.TestMethod, () => x.TestMethod, v => x.TestMethod = v);
        changed |= PatchHelper.Set(value.InputConfigJson, () => x.InputConfigJson, v => x.InputConfigJson = v);
        changed |= PatchHelper.Set(value.AcceptanceRuleJson, () => x.AcceptanceRuleJson, v => x.AcceptanceRuleJson = v);
        changed |= PatchHelper.Set(value.SpecificationText, () => x.SpecificationText, v => x.SpecificationText = v);
        changed |= PatchHelper.Set(value.IsRequired, () => x.IsRequired, v => x.IsRequired = v);
        changed |= PatchHelper.Set(value.SortOrder, () => x.SortOrder, v => x.SortOrder = v);
        return changed;
    }

    public static EvaluationConfigurationDto Merge(EvaluationConfigurationDto current, EvaluationConfigurationRequest request, MeasurementMethod? selectedMethod)
    {
        string[] allowed = ["unit", "measurementMethodId", "testMethod", "inputConfigJson", "acceptanceRuleJson", "specificationText"];
        if (request.ClearFields is null || request.ClearFields.Any(x => !allowed.Contains(x)))
            throw new EvaluationRequestException("clearFields chứa field không hợp lệ.");
        foreach (var field in request.ClearFields)
        {
            var hasValue = field switch
            {
                "unit" => request.Unit is not null,
                "measurementMethodId" => request.MeasurementMethodId.HasValue,
                "testMethod" => request.TestMethod is not null,
                "inputConfigJson" => request.InputConfigJson is not null,
                "acceptanceRuleJson" => request.AcceptanceRuleJson is not null,
                "specificationText" => request.SpecificationText is not null,
                _ => false
            };
            if (hasValue) throw new EvaluationRequestException($"Không vừa gửi value vừa clear {field}.");
        }
        string? Resolve(string? value, string? old, string field) => request.ClearFields.Contains(field) ? null : value is null ? old : string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        var clearMethod = request.ClearFields.Contains("measurementMethodId");
        var merged = current with
        {
            DataType = request.DataType ?? current.DataType,
            Unit = Resolve(request.Unit, current.Unit, "unit"),
            MeasurementMethodId = clearMethod ? null : selectedMethod?.Id ?? current.MeasurementMethodId,
            MeasurementMethodCode = clearMethod ? null : selectedMethod?.Code ?? current.MeasurementMethodCode,
            TestMethod = Resolve(request.TestMethod, selectedMethod?.Name ?? current.TestMethod, "testMethod"),
            InputConfigJson = Resolve(request.InputConfigJson, current.InputConfigJson, "inputConfigJson"),
            AcceptanceRuleJson = Resolve(request.AcceptanceRuleJson, current.AcceptanceRuleJson, "acceptanceRuleJson"),
            SpecificationText = Resolve(request.SpecificationText, current.SpecificationText, "specificationText"),
            IsRequired = request.IsRequired ?? current.IsRequired,
            SortOrder = current.SortOrder // Thứ tự do backend quản lý, không nhận override từ FE.
        };
        var error = EvaluationStandardDefaultsRules.Validate(merged.DataType, merged.Unit, merged.InputConfigJson, merged.AcceptanceRuleJson);
        if (merged.SortOrder < 0 || error is not null) throw new EvaluationRequestException(error ?? "SortOrder không được âm.");
        return merged with
        {
            InputConfigJson = EvaluationStandardDefaultsRules.NormalizeJson(merged.InputConfigJson),
            AcceptanceRuleJson = EvaluationStandardDefaultsRules.NormalizeJson(merged.AcceptanceRuleJson)
        };
    }
}
