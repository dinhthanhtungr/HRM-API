using System.Data.Common;
using System.Text.Json;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Features.InternalMail.Queries.GetConversations;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using NpgsqlTypes;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed class InternalConversationNotificationUnreadTests
{
    [Fact]
    public void UnreadOnly_CanComposeNotificationCountsBeforePagination()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var employeeId = Guid.NewGuid();
        var counts = db.QueryInternalConversationNotificationUnreadCounts(Guid.NewGuid(), employeeId);
        var sql = db.InternalConversationParticipants
            .Where(InternalConversationUnreadFilter.ForEmployee(employeeId, counts))
            .OrderBy(participant => participant.InternalConversationId).Skip(50).Take(50).ToQueryString();
        Assert.Contains("EXISTS", sql);
        Assert.Contains("LIMIT", sql);
    }

    [Fact]
    public void UnreadOnly_IncludesNotificationWithoutMessageReadStateAndPreservesMessageUnread()
    {
        var employee = Guid.NewGuid();
        var notificationConversation = new InternalConversationParticipant
        {
            InternalConversationId = Guid.NewGuid(), Conversation = new InternalConversation()
        };
        var messageConversation = new InternalConversationParticipant
        {
            InternalConversationId = Guid.NewGuid(), Conversation = new InternalConversation()
        };
        var message = new InternalMessage { SenderEmployeeId = Guid.NewGuid() };
        message.ReadStates.Add(new InternalMessageReadState { EmployeeId = employee, IsRead = false });
        messageConversation.Conversation.Messages.Add(message);
        var counts = new[] { new InternalConversationNotificationUnreadCount
        {
            ConversationId = notificationConversation.InternalConversationId, UnreadCount = 1
        } }.AsQueryable();
        var filter = InternalConversationUnreadFilter.ForEmployee(employee, counts).Compile();
        Assert.True(filter(notificationConversation));
        Assert.True(filter(messageConversation));
        message.ReadStates.Single().IsRead = true;
        Assert.False(filter(messageConversation));
        Assert.False(InternalConversationUnreadFilter.ForEmployee(employee,
            Array.Empty<InternalConversationNotificationUnreadCount>().AsQueryable()).Compile()(notificationConversation));
    }

    // Execute the production query against JSON-derived fixtures on the real provider.
    // No table, application row or schema is created/changed, even temporarily.
    [NotificationPostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Counts_AreIndependentOfFeedPagesAndEnforceInboxAndParticipantScope(bool restrictConversation)
    {
        var company = Guid.NewGuid();
        var employee = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var wrongCompany = Guid.NewGuid();
        var inactiveConversation = Guid.NewGuid();
        var inactiveParticipant = Guid.NewGuid();
        var nonParticipant = Guid.NewGuid();
        var notifications = new List<object>();
        var states = new List<object>();
        void Add(object? payload, bool read = false, bool archived = false, Guid? recipient = null, Guid? tenant = null)
        {
            var id = Guid.NewGuid();
            notifications.Add(new { id, company_id = tenant ?? company, payload_json = payload });
            states.Add(new { notification_id = id, user_id = recipient ?? employee, is_read = read, is_archived = archived });
        }

        // More than three feed pages of already-read entries precede both unread notifications.
        for (var i = 0; i < 110; i++) Add(new { conversationId = first }, read: true);
        Add(new { conversationId = first });
        Add(new { ConversationId = second.ToString().ToUpperInvariant() });
        Add(new { CONVERSATIONID = second });
        Add(new { conversationId = first }, archived: true);
        Add(new { conversationId = first }, recipient: Guid.NewGuid());
        Add(new { conversationId = first }, tenant: Guid.NewGuid());
        Add(new { conversationId = wrongCompany });
        Add(new { conversationId = inactiveConversation });
        Add(new { conversationId = inactiveParticipant });
        Add(new { conversationId = nonParticipant });
        Add(new { conversationId = "not-a-guid" });
        Add(new { conversationId = (string?)null });
        Add(new[] { "legacy-array" });
        Add(null);

        object Conversation(Guid id, Guid? tenant = null, bool active = true) => new
        {
            InternalConversationId = id, CompanyId = tenant ?? company, IsActive = active
        };
        object Participant(Guid id, bool active = true) => new
        {
            InternalConversationId = id, EmployeeId = employee, IsActive = active
        };
        var fixtures = new FixtureTables(notifications, states,
            [Conversation(first), Conversation(second), Conversation(wrongCompany, Guid.NewGuid()),
                Conversation(inactiveConversation, active: false), Conversation(inactiveParticipant), Conversation(nonParticipant)],
            [Participant(first), Participant(second), Participant(wrongCompany), Participant(inactiveConversation),
                Participant(inactiveParticipant, active: false)]);
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("HRM_NOTIFICATION_TEST_CONNECTION")!)
            .AddInterceptors(fixtures).Options);
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY");

        var query = db.QueryInternalConversationNotificationUnreadCounts(company, employee);
        if (restrictConversation) query = query.Where(item => item.ConversationId == second);
        var actual = await query.ToDictionaryAsync(item => item.ConversationId, item => item.UnreadCount);
        Assert.Equal(restrictConversation ? 1 : 2, actual.Count);
        Assert.Equal(2, actual[second]);
        if (!restrictConversation) Assert.Equal(1, actual[first]);
        Assert.Empty(await db.QueryInternalConversationNotificationUnreadCounts(Guid.NewGuid(), employee).ToListAsync());
        Assert.Empty(await db.QueryInternalConversationNotificationUnreadCounts(company, Guid.NewGuid()).ToListAsync());
        await transaction.RollbackAsync();
    }

    private sealed class FixtureTables(
        List<object> notifications, List<object> states, object[] conversations, object[] participants) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Replace("notification.notification_user_states", "fixture_states", states,
                "notification_id uuid, user_id uuid, is_read boolean, is_archived boolean");
            Replace("notification.notifications", "fixture_notifications", notifications,
                "id uuid, company_id uuid, payload_json jsonb");
            Replace("\"InternalMail\".\"InternalConversations\"", "fixture_conversations", conversations,
                "\"InternalConversationId\" uuid, \"CompanyId\" uuid, \"IsActive\" boolean");
            Replace("\"InternalMail\".\"InternalConversationParticipants\"", "fixture_participants", participants,
                "\"InternalConversationId\" uuid, \"EmployeeId\" uuid, \"IsActive\" boolean");
            return ValueTask.FromResult(result);

            void Replace(string table, string parameterName, object data, string columns)
            {
                if (!command.CommandText.Contains(table, StringComparison.Ordinal)) return;
                // All SQL identifiers/types here are fixed test constants; fixture values are parameters.
                command.CommandText = command.CommandText.Replace(table,
                    $"(SELECT * FROM jsonb_to_recordset(@{parameterName}) AS fixture({columns}))", StringComparison.Ordinal);
                command.Parameters.Add(new NpgsqlParameter(parameterName, NpgsqlDbType.Jsonb)
                {
                    Value = JsonSerializer.Serialize(data)
                });
            }
        }
    }
}
