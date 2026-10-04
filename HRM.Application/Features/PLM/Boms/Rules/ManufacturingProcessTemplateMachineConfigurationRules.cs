using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Entities.BomSchema;

namespace HRM.Application.Features.PLM.Boms.Rules;

internal static class ManufacturingProcessTemplateMachineConfigurationRules
{
    internal static bool HasSameSharedConfiguration(
        ManufacturingProcessTemplateStageMachineWriteDto left,
        ManufacturingProcessTemplateStageMachineWriteDto right)
        => SameText(left.ConfigurationGroupName, right.ConfigurationGroupName, StringComparison.OrdinalIgnoreCase) &&
           SameText(left.Note, right.Note, StringComparison.Ordinal) &&
           HaveSameParameters(left.Parameters, right.Parameters);

    internal static bool HasSameSharedConfiguration(
        ManufacturingProcessTemplateStageMachine left,
        ManufacturingProcessTemplateStageMachine right)
        => SameText(left.ConfigurationGroupName, right.ConfigurationGroupName, StringComparison.OrdinalIgnoreCase) &&
           SameText(left.Note, right.Note, StringComparison.Ordinal) &&
           HaveSameParameters(left.Parameters, right.Parameters);

    private static bool HaveSameParameters(
        IEnumerable<ManufacturingProcessTemplateStageMachineParameterWriteDto> left,
        IEnumerable<ManufacturingProcessTemplateStageMachineParameterWriteDto> right)
    {
        var leftValues = left.OrderBy(x => x.SequenceNo).ThenBy(x => x.ParameterCode, StringComparer.OrdinalIgnoreCase).ToList();
        var rightValues = right.OrderBy(x => x.SequenceNo).ThenBy(x => x.ParameterCode, StringComparer.OrdinalIgnoreCase).ToList();
        return HaveSameParameters(leftValues, rightValues, static x => x);
    }

    private static bool HaveSameParameters(
        IEnumerable<ManufacturingProcessTemplateStageMachineParameter> left,
        IEnumerable<ManufacturingProcessTemplateStageMachineParameter> right)
    {
        var leftValues = left.OrderBy(x => x.SequenceNo).ThenBy(x => x.ParameterCode, StringComparer.OrdinalIgnoreCase).ToList();
        var rightValues = right.OrderBy(x => x.SequenceNo).ThenBy(x => x.ParameterCode, StringComparer.OrdinalIgnoreCase).ToList();
        if (leftValues.Count != rightValues.Count)
            return false;

        for (var index = 0; index < leftValues.Count; index++)
        {
            var a = leftValues[index];
            var b = rightValues[index];
            if (!SameParameter(
                    a.ParameterCode, a.ParameterName, a.TargetValue, a.MinValue, a.MaxValue, a.Unit, a.IsRequired, a.SequenceNo, a.Note,
                    b.ParameterCode, b.ParameterName, b.TargetValue, b.MinValue, b.MaxValue, b.Unit, b.IsRequired, b.SequenceNo, b.Note))
                return false;
        }

        return true;
    }

    private static bool HaveSameParameters<T>(IReadOnlyList<T> left, IReadOnlyList<T> right, Func<T, ManufacturingProcessTemplateStageMachineParameterWriteDto> selector)
    {
        if (left.Count != right.Count)
            return false;

        for (var index = 0; index < left.Count; index++)
        {
            var a = selector(left[index]);
            var b = selector(right[index]);
            if (!SameParameter(
                    a.ParameterCode, a.ParameterName, a.TargetValue, a.MinValue, a.MaxValue, a.Unit, a.IsRequired, a.SequenceNo, a.Note,
                    b.ParameterCode, b.ParameterName, b.TargetValue, b.MinValue, b.MaxValue, b.Unit, b.IsRequired, b.SequenceNo, b.Note))
                return false;
        }

        return true;
    }

    private static bool SameParameter(
        string leftCode, string leftName, decimal? leftTarget, decimal? leftMin, decimal? leftMax, string leftUnit, bool leftRequired, int leftSequence, string? leftNote,
        string rightCode, string rightName, decimal? rightTarget, decimal? rightMin, decimal? rightMax, string rightUnit, bool rightRequired, int rightSequence, string? rightNote)
        => string.Equals(leftCode.Trim(), rightCode.Trim(), StringComparison.OrdinalIgnoreCase) &&
           string.Equals(leftName.Trim(), rightName.Trim(), StringComparison.Ordinal) &&
           leftTarget == rightTarget && leftMin == rightMin && leftMax == rightMax &&
           string.Equals(leftUnit.Trim(), rightUnit.Trim(), StringComparison.OrdinalIgnoreCase) &&
           leftRequired == rightRequired && leftSequence == rightSequence &&
           SameText(leftNote, rightNote, StringComparison.Ordinal);

    private static bool SameText(string? left, string? right, StringComparison comparison)
        => string.Equals(Normalize(left), Normalize(right), comparison);

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
