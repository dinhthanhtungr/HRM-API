using HRM.Application.Commons.Models;

namespace HRM.Application.Features.PLM.SampleRequests.SampleTrials;

internal static class SampleRequestDraftTrialUpsertContract
{
    private static readonly IReadOnlySet<string> AllowedFields =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            SampleRequestSampleTrialPatchFields.FormulaId,
            SampleRequestSampleTrialPatchFields.BatchNo,
            SampleRequestSampleTrialPatchFields.DeliveredSampleQuantityKg,
            SampleRequestSampleTrialPatchFields.RequestDeliveryDate,
            SampleRequestSampleTrialPatchFields.ExpectedDeliveryDate,
            SampleRequestSampleTrialPatchFields.DeliveryMethod,
            SampleRequestSampleTrialPatchFields.LabNote
        };

    private static readonly IReadOnlySet<string> TrialFields =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            SampleRequestSampleTrialPatchFields.FormulaId,
            SampleRequestSampleTrialPatchFields.FormulaExternalId,
            SampleRequestSampleTrialPatchFields.BatchNo,
            SampleRequestSampleTrialPatchFields.DeliveredSampleQuantityKg,
            SampleRequestSampleTrialPatchFields.DeliveryMethod,
            SampleRequestSampleTrialPatchFields.LabNote
        };

    public static OperationResult<IReadOnlySet<string>> ValidateAndNormalizeClearFields(
        IReadOnlyCollection<string>? clearFields,
        IReadOnlyCollection<string> fieldsWithValues)
    {
        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawField in clearFields ?? Array.Empty<string>())
        {
            var field = rawField?.Trim();
            if (string.IsNullOrEmpty(field) || !AllowedFields.Contains(field))
            {
                return OperationResult<IReadOnlySet<string>>.Fail(
                    $"Clear field '{rawField}' is not supported for a draft trial.");
            }

            normalized.Add(field);
        }

        var conflict = fieldsWithValues.FirstOrDefault(normalized.Contains);
        return conflict is null
            ? OperationResult<IReadOnlySet<string>>.Ok(normalized)
            : OperationResult<IReadOnlySet<string>>.Fail(
                $"Field '{conflict}' cannot be updated and cleared in the same request.");
    }

    public static bool HasTrialMutation(
        IEnumerable<string> fieldsWithValues,
        IEnumerable<string> clearFields)
        => fieldsWithValues.Concat(clearFields).Any(TrialFields.Contains);
}
