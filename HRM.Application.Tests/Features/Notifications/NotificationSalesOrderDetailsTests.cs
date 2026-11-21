using System.Text.Json;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;

namespace HRM.Application.Tests.Features.Notifications;

public sealed class NotificationSalesOrderDetailsTests
{
    [Fact]
    public void LegacyRootIdOverridesCodeFallback()
    {
        var id = Guid.NewGuid();
        var item = new NotificationDto {
            Context = new NotificationContextDto { AggregateType = "MerchandiseOrder", AggregateCode = "DHG-old" },
            PayloadJson = JsonSerializer.Serialize(new { merchandiseOrderId = id })
        };
        var result = NotificationService.SalesOrderReferences(item);
        Assert.Equal(new[] { id }, result.Ids);
        Assert.Empty(result.Codes);
    }

    [Fact]
    public void MultipleOrdersUseIdsAndOnlyFallbackToCodeWhenIdMissing()
    {
        var id = Guid.NewGuid();
        var item = new NotificationDto { PayloadJson = JsonSerializer.Serialize(new {
            merchandiseOrders = new object[] {
                new { id, externalId = "DHG-1" }, new { externalId = "DHG-2" }, new { id, externalId = "DHG-1" }
            }
        }) };
        var result = NotificationService.SalesOrderReferences(item);
        Assert.Equal(new[] { id }, result.Ids);
        Assert.Equal(new[] { "DHG-2" }, result.Codes);
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")] 
    public void NeverGuessesOrderFromTitleOrMessage(string payload)
    {
        var result = NotificationService.SalesOrderReferences(new NotificationDto {
            Title = "DHG-1", Message = "DHG-2", PayloadJson = payload
        });
        Assert.Empty(result.Ids);
        Assert.Empty(result.Codes);
    }
}
