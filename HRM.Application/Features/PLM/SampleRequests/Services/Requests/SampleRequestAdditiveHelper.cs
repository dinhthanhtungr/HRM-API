using HRM.Domain.ReferenceData.SampleRequests;

namespace HRM.Application.Features.PLM.SampleRequests;

internal static class SampleRequestAdditiveHelper
{
    public static string? ResolveGroupCode(string? additiveCode)
    {
        if (string.IsNullOrWhiteSpace(additiveCode))
        {
            return null;
        }

        var trimmed = additiveCode.Trim();
        var segments = trimmed.Split('_', StringSplitOptions.RemoveEmptyEntries);

        return segments.Length > 0 ? segments[0] : trimmed;
    }

    public static string? ResolveDisplayName(string? additiveCode)
    {
        if (string.IsNullOrWhiteSpace(additiveCode))
        {
            return null;
        }

        return SampleRequestReferenceData.FindAdditive(additiveCode)?.DisplayName
            ?? additiveCode.Trim();
    }
}
