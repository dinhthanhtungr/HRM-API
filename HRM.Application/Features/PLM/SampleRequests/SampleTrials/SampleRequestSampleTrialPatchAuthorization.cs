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
        bool canUpdateExpectedPriceQuoteDate,
        IEnumerable<string> dirtyFields)
    {
        var fields = dirtyFields
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (!canUpdateExpectedPriceQuoteDate &&
            fields.Contains(
                SampleRequestSampleTrialPatchFields.ExpectedPriceQuoteDate,
                StringComparer.OrdinalIgnoreCase))
        {
            return "You are not allowed to update expectedPriceQuoteDate.";
        }

        if (canUpdateTechnicalFields)
        {
            return null;
        }

        if (!canUpdateCustomerFeedback)
        {
            return "You are not allowed to update sample trials.";
        }

        var forbiddenFields = fields
            .Where(field => !CustomerFeedbackFields.Contains(field))
            .OrderBy(field => field)
            .ToArray();

        return forbiddenFields.Length == 0
            ? null
            : $"Sale users can only update customerReplyStatus and customerReplyNote. Forbidden fields: {string.Join(", ", forbiddenFields)}.";
    }
}
