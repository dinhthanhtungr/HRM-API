using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.NotificationHub.Dtos;
using HRM.Application.Features.NotificationHub.Queries.GetItems;
using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Application.Tests.Features.NotificationHub;

public sealed class NotificationHubReadModelTests
{
    [Fact]
    public void Cursor_RoundTripsFeedKeyset()
    {
        var createdDate = new DateTime(2026, 9, 9, 10, 30, 15, DateTimeKind.Local);
        var notificationId = Guid.NewGuid();

        var cursor = NotificationHubCursor.Encode(createdDate, notificationId);
        var decoded = NotificationHubCursor.TryDecode(cursor, out var decodedDate, out var decodedId);

        Assert.True(decoded);
        Assert.Equal(createdDate, decodedDate);
        Assert.Equal(notificationId, decodedId);
    }

    [Fact]
    public void Mapper_AddsSampleRequestMetadataOnlyForSampleRequestConversation()
    {
        var relatedId = Guid.NewGuid();
        var snapshot = CreateSnapshot(InternalMailRelatedType.SampleRequest, relatedId);
        var sampleRequestInfo = new SampleRequestConversationInfoDto
        {
            SampleRequestId = relatedId,
            RequestCode = "TP_100",
            CustomerCode = "KH_100",
            CustomerName = "Khách mẫu",
            SaleName = "Sale Mẫu"
        };

        var result = NotificationHubConversationInfoMapper.Map(
            snapshot,
            new Dictionary<Guid, SampleRequestConversationInfoDto> { [relatedId] = sampleRequestInfo },
            new Dictionary<Guid, QuotationConversationInfoDto>());

        Assert.Same(sampleRequestInfo, result.SampleRequestInfo);
        Assert.Null(result.QuotationInfo);
        Assert.Equal("Người gửi · nội dung", result.LastMessageBody);
        Assert.True(result.IsUrgent);
    }

    [Fact]
    public void Mapper_AddsQuotationMetadataOnlyForQuotationConversation()
    {
        var relatedId = Guid.NewGuid();
        var snapshot = CreateSnapshot(InternalMailRelatedType.Quotation, relatedId);
        var quotationInfo = new QuotationConversationInfoDto
        {
            QuotationId = relatedId,
            QuotationCode = "BBG_100",
            CustomerCode = "KH_200",
            CustomerName = "Khách báo giá",
            SaleName = "Sale Báo giá"
        };

        var result = NotificationHubConversationInfoMapper.Map(
            snapshot,
            new Dictionary<Guid, SampleRequestConversationInfoDto>(),
            new Dictionary<Guid, QuotationConversationInfoDto> { [relatedId] = quotationInfo });

        Assert.Same(quotationInfo, result.QuotationInfo);
        Assert.Null(result.SampleRequestInfo);
    }

    private static NotificationHubConversationSnapshot CreateSnapshot(
        InternalMailRelatedType relatedType,
        Guid relatedId) => new(
            Guid.NewGuid(),
            "Tiêu đề",
            relatedType,
            relatedId,
            null,
            "Người gửi",
            "Người gửi · nội dung",
            new DateTime(2026, 9, 9, 10, 30, 0),
            2,
            true);
}
