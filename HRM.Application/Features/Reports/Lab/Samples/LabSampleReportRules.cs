using System.Linq.Expressions;

namespace HRM.Application.Features.Reports.Lab.Samples;

internal static class LabSampleReportRules
{
    public static string? Validate(LabSampleReportFilter filter)
    {
        if (filter.From == default || filter.To == default || filter.To == DateOnly.MaxValue || filter.From > filter.To)
            return "From/To must be valid dates with From <= To.";
        if (filter.To < new DateOnly(1, 6, 1)) return "To must support a complete six-month calendar window.";
        if (filter.RequestType?.Length > 100) return "RequestType cannot exceed 100 characters.";
        if (filter.SaleEmployeeId == Guid.Empty || filter.CustomerId == Guid.Empty || filter.LabReceivedByEmployeeId == Guid.Empty)
            return "Filter ids cannot be empty.";
        return null;
    }

    public static double? LeadTime(DateTime? start, DateTime? end)
        => start.HasValue && end.HasValue && end >= start
            ? Math.Round((end.Value - start.Value).TotalDays, 2, MidpointRounding.AwayFromZero) : null;

    public static bool? Conversion(LabSampleRowDto row)
        => row.CustomerApprovedAt.HasValue ? true : row.ConversionUnknown ? null : false;

    public static Expression<Func<LabSampleRowDto, bool>> Predicate(LabSampleMetric metric) => metric switch
    {
        LabSampleMetric.All => x => true,
        LabSampleMetric.Processing => x => x.Status == "New" || x.Status == "InProgress" || x.Status == "FormulaUpdateRequested",
        LabSampleMetric.Completed => x => x.Status == "Completed",
        LabSampleMetric.Sent => x => x.Status == "SampleSent",
        LabSampleMetric.WaitingFeedback => x => x.WaitingFeedback,
        LabSampleMetric.Accepted => x => x.Accepted,
        LabSampleMetric.Rejected => x => x.Rejected,
        LabSampleMetric.Won => x => x.CustomerApprovedAt != null,
        LabSampleMetric.LeadTimeValid => x => x.ColourAssignedAt != null && x.LabApprovedAt != null && x.LabApprovedAt >= x.ColourAssignedAt,
        LabSampleMetric.LeadTimeMissing => x => x.ColourAssignedAt == null || x.LabApprovedAt == null,
        LabSampleMetric.LeadTimeInvalid => x => x.ColourAssignedAt != null && x.LabApprovedAt != null && x.LabApprovedAt < x.ColourAssignedAt,
        LabSampleMetric.ConversionUnknown => x => x.ConversionUnknown,
        LabSampleMetric.OtherStatus => x => x.Status != "New" && x.Status != "InProgress" && x.Status != "FormulaUpdateRequested"
            && x.Status != "Completed" && x.Status != "SampleSent",
        _ => throw new ArgumentOutOfRangeException(nameof(metric))
    };
}
