using System.Text.Json;
using HRM.Application.Features.InternalMail.Reactions;
using HRM.Domain.Entities.Notifications;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using HRM.Infrastructure.Services.InternalMail;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed class InternalMessageReactionEventsTests
{
    [Fact]
    public void EnqueueOnlyStagesIdSignalInSameUnitOfWorkWithoutNotificationOrReadState()
    {
        // Không kết nối DB: Add chỉ stage event cho SaveChanges của mutation cảm xúc.
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid();
        var conversation = Guid.NewGuid();
        var message = Guid.NewGuid();

        new InternalMessageReactionEvents(db).Enqueue(company, conversation, message);

        var entry = Assert.Single(db.ChangeTracker.Entries());
        Assert.Equal(EntityState.Added, entry.State);
        var signal = Assert.IsType<OutboxMessage>(entry.Entity);
        Assert.Equal(InternalMessageReactionOutbox.Type, signal.Type);
        Assert.Null(signal.ProcessedAt);
        Assert.Equal(0, signal.Attempts);
        Assert.Equal(new InternalMessageReactionEnvelope(company, conversation, message),
            JsonSerializer.Deserialize<InternalMessageReactionEnvelope>(signal.PayloadJson));
        using var payload = JsonDocument.Parse(signal.PayloadJson);
        Assert.Equal(3, payload.RootElement.EnumerateObject().Count());
    }
}
