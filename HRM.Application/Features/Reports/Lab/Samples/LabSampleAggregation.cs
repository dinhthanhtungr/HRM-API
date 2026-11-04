using System.Linq.Expressions;

namespace HRM.Application.Features.Reports.Lab.Samples;

internal sealed class LabSampleAggregate
{
    public int Key { get; init; }
    public int Count { get; init; }
    public int Valid { get; init; }
    public int Missing { get; init; }
    public int Invalid { get; init; }
    public double? Average { get; init; }
    public int Won { get; init; }
    public int Unknown { get; init; }
}

internal static class LabSampleAggregation
{
    public static readonly Expression<Func<IGrouping<int, LabSampleRowDto>, LabSampleAggregate>> Projection = g => new()
    {
        Key = g.Key, Count = g.Count(),
        Valid = g.Count(x => x.ColourAssignedAt != null && x.LabApprovedAt != null && x.LabApprovedAt >= x.ColourAssignedAt),
        Missing = g.Count(x => x.ColourAssignedAt == null || x.LabApprovedAt == null),
        Invalid = g.Count(x => x.ColourAssignedAt != null && x.LabApprovedAt != null && x.LabApprovedAt < x.ColourAssignedAt),
        Average = g.Average(x => x.ColourAssignedAt != null && x.LabApprovedAt != null && x.LabApprovedAt >= x.ColourAssignedAt
            ? (double?)(x.LabApprovedAt.Value - x.ColourAssignedAt.Value).TotalDays : null),
        Won = g.Count(x => x.CustomerApprovedAt != null),
        Unknown = g.Count(x => x.ConversionUnknown)
    };

    public static LabSampleTotals ToDto(LabSampleAggregate? aggregate)
    {
        var a = aggregate ?? new LabSampleAggregate();
        return new LabSampleTotals
        {
            RequestCount = a.Count,
            LeadTimeValidCount = a.Valid, LeadTimeMissingCount = a.Missing, LeadTimeInvalidCount = a.Invalid,
            LeadTimeEligibleCount = a.Valid + a.Missing + a.Invalid,
            LeadTimeUnavailableReason = a.Count == 0 ? "no_requests" : a.Valid == 0 ? "no_valid_dates" : null,
            DevelopmentLeadTimeDays = a.Average.HasValue ? Math.Round(a.Average.Value, 2, MidpointRounding.AwayFromZero) : null,
            WinNumerator = a.Won, WinDenominator = a.Count, ConversionUnknownCount = a.Unknown,
            WinPercentage = a.Count == 0 || a.Unknown > 0 ? null : Math.Round(a.Won * 100m / a.Count, 2, MidpointRounding.AwayFromZero),
            WinUnavailableReason = a.Count == 0 ? "no_requests" : a.Unknown > 0 ? "missing_completed_audit" : null
        };
    }
    public static LabSampleAggregate Combine(IReadOnlyList<LabSampleAggregate> months)
    {
        var valid = months.Sum(x => x.Valid);
        return new()
        {
            Count = months.Sum(x => x.Count),
            Valid = valid, Missing = months.Sum(x => x.Missing), Invalid = months.Sum(x => x.Invalid),
            Average = valid == 0 ? null : months.Sum(x => (x.Average ?? 0) * x.Valid) / valid,
            Won = months.Sum(x => x.Won), Unknown = months.Sum(x => x.Unknown)
        };
    }
}
