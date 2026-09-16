using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed class InternalConversationRelatedInfoMapperTests
{
    [Fact]
    public void UnfilteredList_MapsSampleRequestAndQuotationMetadata()
    {
        var sampleRequestId = Guid.NewGuid();
        var quotationId = Guid.NewGuid();
        var items = new[]
        {
            CreateListItem(InternalMailRelatedType.SampleRequest, sampleRequestId),
            CreateListItem(InternalMailRelatedType.Quotation, quotationId)
        };

        foreach (var item in items)
        {
            InternalConversationRelatedInfoMapper.Apply(
                item,
                new Dictionary<Guid, SampleRequestConversationInfoDto> { [sampleRequestId] = CreateSampleRequestInfo(sampleRequestId) },
                new Dictionary<Guid, QuotationConversationInfoDto> { [quotationId] = CreateQuotationInfo(quotationId) });
        }

        Assert.NotNull(items[0].SampleRequestInfo);
        Assert.Null(items[0].QuotationInfo);
        Assert.NotNull(items[1].QuotationInfo);
        Assert.Null(items[1].SampleRequestInfo);
    }

    [Fact]
    public void SampleRequestFilteredList_MapsOnlySampleRequestInfo()
    {
        var sampleRequestId = Guid.NewGuid();
        var item = CreateListItem(InternalMailRelatedType.SampleRequest, sampleRequestId);

        InternalConversationRelatedInfoMapper.Apply(
            item,
            new Dictionary<Guid, SampleRequestConversationInfoDto> { [sampleRequestId] = CreateSampleRequestInfo(sampleRequestId) },
            new Dictionary<Guid, QuotationConversationInfoDto>());

        Assert.Equal("TP_100", item.SampleRequestInfo?.RequestCode);
        Assert.Equal("KH_100", item.SampleRequestInfo?.CustomerCode);
        Assert.Equal("Sale Mẫu", item.SampleRequestInfo?.SaleName);
        Assert.Null(item.QuotationInfo);
    }

    [Fact]
    public void QuotationFilteredList_MapsOnlyQuotationInfo()
    {
        var quotationId = Guid.NewGuid();
        var item = CreateListItem(InternalMailRelatedType.Quotation, quotationId);

        InternalConversationRelatedInfoMapper.Apply(
            item,
            new Dictionary<Guid, SampleRequestConversationInfoDto>(),
            new Dictionary<Guid, QuotationConversationInfoDto> { [quotationId] = CreateQuotationInfo(quotationId) });

        Assert.Equal("BBG_100", item.QuotationInfo?.QuotationCode);
        Assert.Equal("KH_200", item.QuotationInfo?.CustomerCode);
        Assert.Equal("Sale Báo giá", item.QuotationInfo?.SaleName);
        Assert.Null(item.SampleRequestInfo);
    }

    [Fact]
    public void DetailConversation_MapsMetadataByRelatedType()
    {
        var quotationId = Guid.NewGuid();
        var conversation = new InternalConversationDetailDto
        {
            RelatedType = InternalMailRelatedType.Quotation,
            RelatedId = quotationId
        };

        InternalConversationRelatedInfoMapper.Apply(
            conversation,
            new Dictionary<Guid, SampleRequestConversationInfoDto>(),
            new Dictionary<Guid, QuotationConversationInfoDto> { [quotationId] = CreateQuotationInfo(quotationId) });

        Assert.NotNull(conversation.QuotationInfo);
        Assert.Null(conversation.SampleRequestInfo);
    }

    private static InternalConversationListItemDto CreateListItem(InternalMailRelatedType relatedType, Guid relatedId) => new()
    {
        RelatedType = relatedType,
        RelatedId = relatedId
    };

    private static SampleRequestConversationInfoDto CreateSampleRequestInfo(Guid id) => new()
    {
        SampleRequestId = id,
        RequestCode = "TP_100",
        CustomerCode = "KH_100",
        CustomerName = "Khách mẫu",
        SaleName = "Sale Mẫu"
    };

    private static QuotationConversationInfoDto CreateQuotationInfo(Guid id) => new()
    {
        QuotationId = id,
        QuotationCode = "BBG_100",
        CustomerCode = "KH_200",
        CustomerName = "Khách báo giá",
        SaleName = "Sale Báo giá"
    };
}
