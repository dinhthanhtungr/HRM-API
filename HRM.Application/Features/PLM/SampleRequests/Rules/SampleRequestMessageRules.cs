using System.Globalization;
using System.Text;
using HRM.Application.Features.PLM.Shared.Rules;

namespace HRM.Application.Features.PLM.SampleRequests.Rules;

internal static class SampleRequestMessageRules
{
    public static bool ShouldSuppressMessages(string? requestType, string? customerExternalId)
    {
        return IsInternalRequestType(requestType) ||
               PLMCustomerRules.IsInternalCustomerExternalId(customerExternalId);
    }

    private static bool IsInternalRequestType(string? requestType)
    {
        if (string.IsNullOrWhiteSpace(requestType))
        {
            return false;
        }

        var normalized = NormalizeForBusinessCode(requestType);

        return normalized is "NOIBO" or "INTERNAL" or "PRIVATE";
    }

    private static string NormalizeForBusinessCode(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (!char.IsWhiteSpace(character) && character != '_' && character != '-')
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
