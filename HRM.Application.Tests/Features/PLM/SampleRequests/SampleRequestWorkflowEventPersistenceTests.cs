using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestWorkflowEventPersistenceTests
{
    [Fact]
    public void WorkflowHistoryCannotCascadeAwayWhenItsBusinessSourcesAreDeleted()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var eventType = db.Model.FindEntityType(typeof(SampleRequestWorkflowEvent))!;
        Assert.Equal("SampleRequests", eventType.GetSchema());
        Assert.Equal("SampleRequestWorkflowEvents", eventType.GetTableName());
        Assert.All(eventType.GetForeignKeys(), foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
        Assert.Contains(eventType.GetForeignKeys(), foreignKey => foreignKey.Properties.Single().Name == nameof(SampleRequestWorkflowEvent.SampleRequestId));
        Assert.Contains(eventType.GetForeignKeys(), foreignKey => foreignKey.Properties.Single().Name == nameof(SampleRequestWorkflowEvent.RelatedEventId));
    }

    [Fact]
    public void UnknownHistoricalActualDateAndMissingDeliveryLinksRemainRepresentable()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var eventType = db.Model.FindEntityType(typeof(SampleRequestWorkflowEvent))!;
        Assert.True(eventType.FindProperty(nameof(SampleRequestWorkflowEvent.OccurredAt))!.IsNullable);
        Assert.True(eventType.FindProperty(nameof(SampleRequestWorkflowEvent.RecordedByEmployeeId))!.IsNullable);
        Assert.False(eventType.FindProperty(nameof(SampleRequestWorkflowEvent.RecordedAt))!.IsNullable);
        Assert.True(eventType.FindProperty(nameof(SampleRequestWorkflowEvent.MessageId))!.IsNullable);
        Assert.True(eventType.FindProperty(nameof(SampleRequestWorkflowEvent.NotificationId))!.IsNullable);
    }
}
