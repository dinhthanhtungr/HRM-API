using HRM.Application.Commons.Models;

namespace HRM.Application.Features.PLM.SampleRequests.SampleTrials;

internal static class SampleRequestSampleTrialPatchContract
{
    public static OperationResult<IReadOnlySet<string>> ValidateAndNormalize(
        IReadOnlyList<string>? requestedClearFields,
        IEnumerable<string> fieldsWithValues)
    {
        var clearFields = new HashSet<string>(
            requestedClearFields ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        var unknown = clearFields
            .Where(x => !SampleRequestSampleTrialPatchFields.Allowed.Contains(x))
            .OrderBy(x => x)
            .ToList();
        if (unknown.Count > 0)
        {
            return OperationResult<IReadOnlySet<string>>.Fail(
                $"Unsupported clearFields: {string.Join(", ", unknown)}.");
        }

        var suppliedFields = new HashSet<string>(fieldsWithValues, StringComparer.OrdinalIgnoreCase);
        var conflicts = clearFields
            .Where(suppliedFields.Contains)
            .OrderBy(x => x)
            .ToList();

        return conflicts.Count > 0
            ? OperationResult<IReadOnlySet<string>>.Fail(
                $"Fields cannot be updated and cleared in the same request: {string.Join(", ", conflicts)}.")
            : OperationResult<IReadOnlySet<string>>.Ok(clearFields);
    }
}
