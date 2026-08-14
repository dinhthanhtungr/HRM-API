namespace HRM.Application.Features.PLM.SampleRequests.SampleTrials;

internal static class SampleRequestSampleTrialPatchAuthorization
{
    private static readonly IReadOnlySet<string> CustomerFeedbackFields =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            SampleRequestSampleTrialPatchFields.CustomerReplyStatus,
            SampleRequestSampleTrialPatchFields.CustomerReplyNote
        };

    public static string? Validate(
        bool canUpdateTechnicalFields,
        bool canUpdateCustomerFeedback,
        IEnumerable<string> dirtyFields)
    {
        if (canUpdateTechnicalFields)
        {
            return null;
        }

        if (!canUpdateCustomerFeedback)
        {
            return "You are not allowed to update sample trials.";
        }

        var forbiddenFields = dirtyFields
            .Where(field => !CustomerFeedbackFields.Contains(field))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(field => field)
            .ToArray();

        return forbiddenFields.Length == 0
            ? null
            : $"Sale users can only update customerReplyStatus and customerReplyNote. Forbidden fields: {string.Join(", ", forbiddenFields)}.";
    }
}
