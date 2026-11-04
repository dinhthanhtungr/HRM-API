using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Features.PLM.SampleRequests.SampleTrials;

internal static class SampleRequestSampleTrialMutationRules
{
    public const int MaxBatchNoLength = 100;
    public const int MaxDeliveryMethodLength = 100;
    public const int MaxLabNoteLength = 5000;
    public const int MaxCustomerReplyStatusLength = 50;
    public const int MaxCustomerReplyNoteLength = 5000;

    public static string? Validate(
        decimal? deliveredSampleQuantityKg,
        decimal? additiveRate,
        DateTime? requestReceivedDate,
        DateTime? finishedDate,
        DateTime? sentDate)
    {
        if (deliveredSampleQuantityKg < 0)
        {
            return "DeliveredSampleQuantityKg cannot be negative.";
        }

        if (additiveRate < 0)
        {
            return "AdditiveRate cannot be negative.";
        }

        if (requestReceivedDate.HasValue && finishedDate.HasValue &&
            finishedDate.Value < requestReceivedDate.Value)
        {
            return "FinishedDate cannot be earlier than RequestReceivedDate.";
        }

        if (finishedDate.HasValue && sentDate.HasValue && sentDate.Value < finishedDate.Value)
        {
            return "SentDate cannot be earlier than FinishedDate.";
        }

        return null;
    }

    public static string? ValidateText(string? value, int maxLength, string fieldName)
    {
        if (value is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return $"{fieldName} cannot be blank. Use clearFields to set it to null.";
        }

        return value.Trim().Length > maxLength
            ? $"{fieldName} cannot exceed {maxLength} characters."
            : null;
    }

    public static void PopulateSnapshots(
        SampleRequestSampleTrial trial,
        SampleRequest sampleRequest)
    {
        trial.CustomerNameSnapshot = sampleRequest.Customer?.CustomerName;
        trial.SampleRequestExternalIdSnapshot = sampleRequest.ExternalId;
        trial.ProductNameSnapshot = sampleRequest.Product?.Name;
        trial.ColourCodeSnapshot = sampleRequest.Product?.ColourCode;
        trial.CategoryNameSnapshot = sampleRequest.Product?.Category?.Name;
    }
}
