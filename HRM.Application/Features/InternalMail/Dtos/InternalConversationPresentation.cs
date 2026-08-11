using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Application.Features.InternalMail.Dtos;

internal static class InternalConversationPresentation
{
    private static readonly string[] SampleRequestSubjectPrefixes =
    [
        "Trao đổi yêu cầu phối mẫu ",
        "Trao doi yeu cau phoi mau "
    ];

    public static string BuildDisplayTitle(
        InternalMailRelatedType? relatedType,
        string subject,
        string? relatedExternalId)
    {
        var normalizedSubject = subject.Trim();

        if (relatedType == InternalMailRelatedType.SampleRequest)
        {
            foreach (var prefix in SampleRequestSubjectPrefixes)
            {
                if (normalizedSubject.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var compactTitle = normalizedSubject[prefix.Length..].Trim();
                    if (!string.IsNullOrWhiteSpace(compactTitle))
                    {
                        return compactTitle;
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(normalizedSubject))
        {
            return normalizedSubject;
        }

        return string.IsNullOrWhiteSpace(relatedExternalId)
            ? "Cuộc trao đổi"
            : relatedExternalId.Trim();
    }
}
