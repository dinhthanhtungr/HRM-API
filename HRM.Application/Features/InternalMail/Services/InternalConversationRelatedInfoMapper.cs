using HRM.Application.Features.InternalMail.Dtos;
using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Application.Features.InternalMail.Services;

/// <summary>
/// Applies loaded business metadata to an InternalMail read DTO independent of the inbox filter.
/// </summary>
internal static class InternalConversationRelatedInfoMapper
{
    public static void Apply(
        InternalConversationListItemDto conversation,
        IReadOnlyDictionary<Guid, SampleRequestConversationInfoDto> sampleRequestInfoById,
        IReadOnlyDictionary<Guid, QuotationConversationInfoDto> quotationInfoById)
    {
        Apply(
            conversation.RelatedType,
            conversation.RelatedId,
            sampleRequestInfoById,
            quotationInfoById,
            out var sampleRequestInfo,
            out var quotationInfo);

        conversation.SampleRequestInfo = sampleRequestInfo;
        conversation.QuotationInfo = quotationInfo;
    }

    public static void Apply(
        InternalConversationDetailDto conversation,
        IReadOnlyDictionary<Guid, SampleRequestConversationInfoDto> sampleRequestInfoById,
        IReadOnlyDictionary<Guid, QuotationConversationInfoDto> quotationInfoById)
    {
        Apply(
            conversation.RelatedType,
            conversation.RelatedId,
            sampleRequestInfoById,
            quotationInfoById,
            out var sampleRequestInfo,
            out var quotationInfo);

        conversation.SampleRequestInfo = sampleRequestInfo;
        conversation.QuotationInfo = quotationInfo;
    }

    private static void Apply(
        InternalMailRelatedType? relatedType,
        Guid? relatedId,
        IReadOnlyDictionary<Guid, SampleRequestConversationInfoDto> sampleRequestInfoById,
        IReadOnlyDictionary<Guid, QuotationConversationInfoDto> quotationInfoById,
        out SampleRequestConversationInfoDto? sampleRequestInfo,
        out QuotationConversationInfoDto? quotationInfo)
    {
        sampleRequestInfo = null;
        quotationInfo = null;

        if (relatedId is not { } id)
        {
            return;
        }

        if (relatedType == InternalMailRelatedType.SampleRequest)
        {
            sampleRequestInfoById.TryGetValue(id, out sampleRequestInfo);
        }
        else if (relatedType == InternalMailRelatedType.Quotation)
        {
            quotationInfoById.TryGetValue(id, out quotationInfo);
        }
    }
}
