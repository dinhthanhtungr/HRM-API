using Npgsql;
using NpgsqlTypes;
using Xunit;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    [PostgresGroupStorageTheory]
    [InlineData("orphan")]
    [InlineData("cross-company")]
    [InlineData("nested")]
    [InlineData("self")]
    [InlineData("duplicate")]
    [InlineData("inactive-parent")]
    [InlineData("blank-custom")]
    [InlineData("long-custom")]
    [InlineData("reference-only-type")]
    [InlineData("cross-company-member")]
    [InlineData("rls")]
    public async Task InvalidLegacyDataFailsPreflightWithoutPartialSchemaOrDataChanges(string scenario)
    {
        await using (var connection = await postgres.OpenAsync())
        {
            switch (scenario)
            {
                case "orphan": await InsertLegacyAsync(connection,Guid.NewGuid(),19,Guid.NewGuid(),"Group"); break;
                case "cross-company": await InsertLegacyAsync(connection,Guid.NewGuid(),19,Guid.Parse("20000000-0000-0000-0000-000000000003"),"Group"); break;
                case "nested": await InsertLegacyAsync(connection,Guid.NewGuid(),19,Guid.Parse(Technical),"Group"); break;
                case "self": var self=Guid.NewGuid(); await InsertLegacyAsync(connection,self,19,self,"Group"); break;
                case "duplicate": await InsertLegacyAsync(connection,Guid.NewGuid(),17,Guid.Parse(Root),"Duplicate"); break;
                case "inactive-parent": await postgres.ExecuteAsync("UPDATE \"InternalMail\".\"InternalConversations\" SET \"IsActive\"=false WHERE \"InternalConversationId\"='20000000-0000-0000-0000-000000000001'"); break;
                case "blank-custom": await InsertLegacyAsync(connection,Guid.NewGuid(),19,Guid.Parse(Root),"   "); break;
                case "long-custom": await InsertLegacyAsync(connection,Guid.NewGuid(),19,Guid.Parse(Root),new string('x',101)); break;
                case "reference-only-type": await InsertLegacyAsync(connection,Guid.NewGuid(),20,Guid.Parse(Root),"Invalid type"); break;
                case "cross-company-member": await postgres.ExecuteAsync("INSERT INTO \"InternalMail\".\"InternalConversationParticipants\" (\"InternalConversationId\",\"EmployeeId\") VALUES ('20000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000003')"); break;
                case "rls": await postgres.ExecuteAsync("ALTER TABLE \"InternalMail\".\"InternalConversations\" ENABLE ROW LEVEL SECURITY"); break;
            }
        }
        var before = await ProtectedRowsAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => postgres.ApplyAsync());
        Assert.Equal("P0001",error.SqlState);
        await AssertExpansionAbsentAsync();
        Assert.Equal(before,await ProtectedRowsAsync());
    }

    [PostgresGroupStorageFact]
    public async Task FailureAfterCreatingTableRollsBackTheWholeExpansion()
    {
        await postgres.ExecuteAsync("CREATE FUNCTION \"InternalMail\".\"ValidateConversationGroup\"() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NEW; END $$");
        var before = await ProtectedRowsAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(()=>postgres.ApplyAsync());
        Assert.Equal("42723",error.SqlState);
        await AssertExpansionAbsentAsync();
        Assert.Equal(before,await ProtectedRowsAsync());
    }

    [PostgresGroupStorageFact]
    public async Task ClosedDefaultReleasesSlotButCannotBeReactivatedOverAnotherActiveDefault()
    {
        await postgres.ApplyAsync();
        await postgres.ExecuteAsync("UPDATE \"InternalMail\".\"InternalConversations\" SET \"IsActive\"=false WHERE \"InternalConversationId\"='30000000-0000-0000-0000-000000000001'");
        await using var connection = await postgres.OpenAsync();
        await InsertLegacyAsync(connection,Guid.NewGuid(),17,Guid.Parse(Root),"Replacement");
        var error = await Assert.ThrowsAsync<PostgresException>(()=>postgres.ExecuteAsync("UPDATE \"InternalMail\".\"InternalConversations\" SET \"IsActive\"=true WHERE \"InternalConversationId\"='30000000-0000-0000-0000-000000000001'"));
        Assert.Equal("23505",error.SqlState);
        Assert.False(await ScalarAsync<bool>("SELECT \"IsActive\" FROM \"InternalMail\".\"InternalConversations\" WHERE \"InternalConversationId\"='30000000-0000-0000-0000-000000000001'"));
        Assert.Equal(1L,await ScalarAsync<long>("SELECT count(*) FROM \"InternalMail\".\"InternalConversationGroups\" WHERE \"GroupType\"=1 AND \"IsActive\""));
    }

    [PostgresGroupStorageFact]
    public async Task CustomGroupsAllowRepeatedNamesButKeepDistinctConversationIds()
    {
        await postgres.ApplyAsync();
        await using var connection=await postgres.OpenAsync();
        await InsertLegacyAsync(connection,Guid.NewGuid(),19,Guid.Parse(Root),"Cùng tên");
        await InsertLegacyAsync(connection,Guid.NewGuid(),19,Guid.Parse(Root),"Cùng tên");
        Assert.Equal(2L,await ScalarAsync<long>("SELECT count(DISTINCT \"ConversationId\") FROM \"InternalMail\".\"InternalConversationGroups\" WHERE \"DisplayName\"='Cùng tên'"));
    }

    [PostgresGroupStorageTheory]
    [InlineData("nested")]
    [InlineData("cross-company")]
    [InlineData("orphan")]
    public async Task InvalidBridgeInsertRollsBackConversationItself(string scenario)
    {
        await postgres.ApplyAsync();
        var before=await ProtectedRowsAsync();
        await using var connection=await postgres.OpenAsync();
        var parent = scenario switch { "nested"=>Guid.Parse(Technical), "cross-company"=>Guid.Parse("20000000-0000-0000-0000-000000000003"), _=>Guid.NewGuid() };
        await Assert.ThrowsAsync<PostgresException>(()=>InsertLegacyAsync(connection,Guid.NewGuid(),19,parent,"Not persisted"));
        Assert.Equal(before,await ProtectedRowsAsync());
    }

    [PostgresGroupStorageTheory]
    [InlineData("type")]
    [InlineData("root")]
    [InlineData("company")]
    [InlineData("lifecycle")]
    public async Task DirectMappingChangesCannotAlterStructureOrLifecycle(string change)
    {
        await postgres.ApplyAsync();
        var sql = change switch
        {
            "type"=>"UPDATE \"InternalMail\".\"InternalConversationGroups\" SET \"GroupType\"=1 WHERE \"ConversationId\"='30000000-0000-0000-0000-000000000004'",
            "root"=>"UPDATE \"InternalMail\".\"InternalConversationGroups\" SET \"RootConversationId\"='20000000-0000-0000-0000-000000000002' WHERE \"ConversationId\"='30000000-0000-0000-0000-000000000004'",
            "company"=>"UPDATE \"InternalMail\".\"InternalConversationGroups\" SET \"CompanyId\"='10000000-0000-0000-0000-000000000002' WHERE \"ConversationId\"='30000000-0000-0000-0000-000000000004'",
            _=>"UPDATE \"InternalMail\".\"InternalConversationGroups\" SET \"IsActive\"=false WHERE \"ConversationId\"='30000000-0000-0000-0000-000000000004'"
        };
        var error=await Assert.ThrowsAsync<PostgresException>(()=>postgres.ExecuteAsync(sql));
        Assert.Equal("P0001",error.SqlState);
    }

    private static async Task InsertLegacyAsync(NpgsqlConnection connection, Guid id, int type, Guid root, string name, NpgsqlTransaction? transaction = null)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO "InternalMail"."InternalConversations"
                ("InternalConversationId","CompanyId","Subject","RelatedType","RelatedId","CreatedBy")
            VALUES (@id,@company,@name,@type,@root,@creator)
            """,connection,transaction);
        command.Parameters.AddWithValue("id",id);
        command.Parameters.AddWithValue("company",Guid.Parse(Company));
        command.Parameters.AddWithValue("name",NpgsqlDbType.Text,name);
        command.Parameters.AddWithValue("type",type);
        command.Parameters.AddWithValue("root",root);
        command.Parameters.AddWithValue("creator",Guid.Parse(Employee));
        await command.ExecuteNonQueryAsync();
    }
}
