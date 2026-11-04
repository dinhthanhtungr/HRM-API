using System.Data;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    // GROUP-STORAGE-TRANSITION: audit-only — verifies both export branches against isolated synthetic data.
    [PostgresGroupStorageFact]
    public async Task ReadOnlyExcelExportPreservesGroupIdsAndCountsBeforeAndAfterMapping()
    {
        await postgres.ExecuteAsync("""
            DROP SCHEMA IF EXISTS "SampleRequests" CASCADE;
            CREATE SCHEMA "SampleRequests";
            CREATE TABLE "SampleRequests"."SampleRequests" ("SampleRequestId" uuid, "CompanyId" uuid, "ProductId" uuid, "ExternalId" text);
            CREATE TABLE "SampleRequests"."Products" ("ProductId" uuid, "CompanyId" uuid, "ColourCode" text);
            INSERT INTO "SampleRequests"."SampleRequests" VALUES
              ('90000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','TP_fixture');
            INSERT INTO "SampleRequests"."Products" VALUES
              ('a0000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','COLOR_fixture');
            """);
        var before = await ExportRowsAsync(false);
        Assert.Equal(7, before.Count); // Includes inactive technical and both separate custom groups; excludes quotation.
        Assert.All(before, row => Assert.False(row.GetProperty("mapping_exists").GetBoolean()));
        Assert.Equal(3, before.Sum(row => row.GetProperty("total_messages").GetInt32()));
        Assert.Equal(1, before.Sum(row => row.GetProperty("deleted_messages").GetInt32()));
        await postgres.ApplyAsync();
        var after = await ExportRowsAsync(true);
        Assert.Equal(before.Select(row => row.GetProperty("conversation_id").GetString()), after.Select(row => row.GetProperty("conversation_id").GetString()));
        Assert.All(after, row => Assert.True(row.GetProperty("mapping_matches").GetBoolean()));
        Assert.Equal(before.Select(row => row.GetProperty("total_messages").GetInt32()), after.Select(row => row.GetProperty("total_messages").GetInt32()));
        Assert.Contains(after, row => row.GetProperty("colour_code").GetString() == "COLOR_fixture");
    }

    private async Task<List<JsonElement>> ExportRowsAsync(bool hasMapping)
    {
        const string populated = """
            SELECT "ConversationId","CompanyId","RootConversationId","GroupType","DisplayName","IsActive" FROM "InternalMail"."InternalConversationGroups"
            """;
        const string empty = """SELECT NULL::uuid AS "ConversationId",NULL::uuid AS "CompanyId",NULL::uuid AS "RootConversationId",NULL::smallint AS "GroupType",NULL::text AS "DisplayName",NULL::boolean AS "IsActive" WHERE false""";
        var sql = (await File.ReadAllTextAsync(Path.Combine(postgres.RepositoryRoot, "tools", "internal-mail-group-storage", "export.sql")))
            .Replace("__MAPPING_SOURCE__", hasMapping ? populated : empty);
        await using var connection = await postgres.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await using (var guard = new NpgsqlCommand("SET TRANSACTION READ ONLY; SET LOCAL statement_timeout='10s'", connection, transaction))
            await guard.ExecuteNonQueryAsync();
        var rows = new List<JsonElement>();
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync())
            {
                using var json = JsonDocument.Parse(reader.GetString(0));
                rows.Add(json.RootElement.Clone());
            }
        await transaction.RollbackAsync();
        return rows;
    }
}
