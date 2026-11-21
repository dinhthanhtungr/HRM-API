using System.Diagnostics;
using System.Text.Json;
using HRM.Application.Features.InternalMail.Services;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    [PostgresGroupStorageTheory]
    [InlineData("President")]
    [InlineData("Developer")]
    [InlineData("LabUser")]
    [InlineData("SaleUser")]
    public async Task EnabledReactionSummaryMatchesScopedBaseline(string role)
    {
        await PresidentBusinessSchemaAsync();
        // All writes target the guarded ephemeral PostgreSQL fixture only.
        await postgres.ExecuteAsync("""
            CREATE TABLE "InternalMail"."InternalMessageReactions" ("MessageId" uuid, "EmployeeId" uuid);
            INSERT INTO "InternalMail"."InternalMessages" ("InternalMessageId","InternalConversationId","SenderEmployeeId","Body")
            VALUES ('49000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000001',
                '00000000-0000-0000-0000-000000000001','Synthetic');
            INSERT INTO "InternalMail"."InternalMessageReactions" VALUES
                ('49000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000002');
            INSERT INTO "InternalMail"."InternalConversationParticipants" ("InternalConversationId","EmployeeId")
            VALUES ('30000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000001') ON CONFLICT DO NOTHING;
            """);
        await using var connection = await postgres.OpenAsync();
        await using (var command = new Npgsql.NpgsqlCommand("""
            INSERT INTO notification.notifications (id,company_id,payload_json,created_date,topic)
            VALUES ('59000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001',
                '{"conversationId":"30000000-0000-0000-0000-000000000001","messageId":"49000000-0000-0000-0000-000000000001"}',now(),@topic);
            INSERT INTO notification.notification_user_states (notification_id,user_id,is_read,is_archived)
            VALUES ('59000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000001',false,false);
            """, connection))
        {
            command.Parameters.AddWithValue("topic", (int)InternalMailPersonalActivity.ReactionTopics[0]);
            await command.ExecuteNonQueryAsync();
        }
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        db.Database.SetCommandTimeout(10);
        var areas = new InternalMailAreaAccessService(db, new UnreadPerformanceUser(role), reactionOptions:
            Options.Create(new InternalMailReactionOptions { Enabled = true }));
        var before = new List<double>(); var after = new List<double>();
        async Task<int> Run(bool old, bool record)
        {
            var watch = Stopwatch.StartNew();
            var topics = InternalMailPersonalActivity.ReactionTopics;
            var value = old ? await areas.States().CountAsync(s => !s.IsRead && !s.IsArchived && topics.Contains(s.Notification.Topic))
                : await areas.PersonalReactionUnreadCountAsync(default);
            if (record) (old ? before : after).Add(watch.Elapsed.TotalMilliseconds);
            return value;
        }
        Assert.Equal(1, await Run(true, false)); Assert.Equal(1, await Run(false, false));
        for (var i = 0; i < 5; i++) Assert.Equal(await Run(i % 2 == 0, true), await Run(i % 2 != 0, true));
        var folder = Path.Combine(postgres.RepositoryRoot, "artifacts", "reaction-summary-comparison");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, role + ".json"), JsonSerializer.Serialize(new {
            role, matched = true, beforeMedianMs = before.Order().ElementAt(2), afterMedianMs = after.Order().ElementAt(2),
            beforeMin = before.Min(), beforeMax = before.Max(), afterMin = after.Min(), afterMax = after.Max() }));
        await postgres.ExecuteAsync("UPDATE notification.notification_user_states SET is_read=true;");
        Assert.Equal(0, await Run(true, false)); Assert.Equal(0, await Run(false, false));
    }
}
