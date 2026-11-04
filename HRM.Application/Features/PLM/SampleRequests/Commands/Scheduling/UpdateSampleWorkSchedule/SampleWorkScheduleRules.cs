namespace HRM.Application.Features.PLM.SampleRequests.Commands.UpdateSampleWorkSchedule;

internal static class SampleWorkScheduleRules
{
    internal const string DeliveryDate = "expectedDeliveryDate";
    internal const string QuoteDate = "expectedPriceQuoteDate";
    internal const string AuditReason = "SampleWorkScheduleUpdated";

    internal static string? Validate(UpdateSampleWorkScheduleCommand command)
    {
        if (command.SampleRequestId == Guid.Empty) return "Sample request is required.";
        if (command.ClearFields is { Length: > 2 } || command.ClearFields?.Any(field => field is not DeliveryDate and not QuoteDate) == true)
            return "Only expectedDeliveryDate and expectedPriceQuoteDate can be cleared.";
        if (command.ExpectedDeliveryDate.HasValue && command.ClearFields?.Contains(DeliveryDate) == true ||
            command.ExpectedPriceQuoteDate.HasValue && command.ClearFields?.Contains(QuoteDate) == true)
            return "A schedule date cannot be set and cleared together.";
        if (command.ExpectedDeliveryDate is { Year: < 1900 } or { Year: > 9998 } ||
            command.ExpectedPriceQuoteDate is { Year: < 1900 } or { Year: > 9998 })
            return "Schedule date is outside the supported range.";
        return null;
    }

    internal static DateTime? Resolve(DateTime? current, DateTime? value, string field, string[]? clearFields)
        => clearFields?.Contains(field) == true ? null : value.HasValue
            ? DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Unspecified) : current;
}
