using System.Text.Json;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.Notifications;

public sealed class NotificationDeliveryStatusRulesTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RecognizesLegacyDeliveryShapeWithoutDependingOnTitle(bool paused)
    {
        var p = Payload(paused);
        Assert.True(NotificationDeliveryStatusRules.IsLegacyPayload(JsonSerializer.Serialize(p)));
        p.Remove("deliveryPauseReason");
        Assert.False(NotificationDeliveryStatusRules.IsLegacyPayload(JsonSerializer.Serialize(p)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("invalid")]
    [InlineData("{\"isDeliveryPaused\":true}")]
    public void DoesNotClassifyUnrelatedOrMalformedLegacyUpdates(string? payload)
        => Assert.False(NotificationDeliveryStatusRules.IsLegacyPayload(payload));

    [Fact]
    public void LegacyPredicateTranslatesToJsonbBeforePagingWithoutDatabaseAccess()
    {
        using var db = new TranslationContext(new DbContextOptionsBuilder<TranslationContext>()
            .UseNpgsql("Host=localhost;Database=translation_only").Options);
        var sql = NotificationDeliveryStatusRules.Filter(db.Set<Notification>())
            .Where(n => n.CompanyId == Guid.Empty).OrderBy(n => n.CreatedDate).Take(30).ToQueryString();
        Assert.Contains("@>", sql);
        Assert.Contains("deliveryPauseReason", sql);
        Assert.Contains("isDeliveryPaused", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("company_id", sql);
    }

    private static Dictionary<string, object?> Payload(bool paused) => new()
    {
        ["isDeliveryPaused"] = paused, ["merchandiseOrderId"] = Guid.NewGuid(),
        ["merchandiseOrderCode"] = "DHG_TEST", ["deliveryPausedFrom"] = null,
        ["deliveryPausedTo"] = null, ["deliveryPauseReason"] = "Synthetic reason", ["deliveryPauseType"] = null
    };

    private sealed class TranslationContext(DbContextOptions<TranslationContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder model)
        {
            var e = model.Entity<Notification>();
            e.ToTable("notifications", "notification");
            e.HasKey(n => n.Id);
            e.Property(n => n.CompanyId).HasColumnName("company_id");
            e.Property(n => n.PayloadJson).HasColumnType("jsonb");
            e.Ignore(n => n.Company);
            e.Ignore(n => n.CreatedByEmployeeNavigation);
            e.Ignore(n => n.Recipients);
            e.Ignore(n => n.UserStates);
        }
    }
}
