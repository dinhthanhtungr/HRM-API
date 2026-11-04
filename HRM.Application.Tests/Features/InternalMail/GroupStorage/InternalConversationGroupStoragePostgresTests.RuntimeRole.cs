using Npgsql;
using Xunit;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    [PostgresGroupStorageFact]
    public async Task DistinctRuntimeRoleNeedsOnlyExplicitNewTableGrantsForLegacyBridge()
    {
        await postgres.ApplyAsync();
        await postgres.ExecuteAsync("""
            DO $$ BEGIN
              IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='group_storage_fixture_app') THEN
                CREATE ROLE group_storage_fixture_app NOLOGIN;
              END IF;
            END $$;
            GRANT USAGE ON SCHEMA "InternalMail" TO group_storage_fixture_app;
            GRANT SELECT,INSERT,UPDATE ON "InternalMail"."InternalConversations" TO group_storage_fixture_app;
            """);
        await using var runtime=await postgres.OpenAsync();
        await using (var role=new NpgsqlCommand("SET ROLE group_storage_fixture_app",runtime)) await role.ExecuteNonQueryAsync();
        var before=await ProtectedRowsAsync();
        var denied=await Assert.ThrowsAsync<PostgresException>(()=>InsertLegacyAsync(runtime,Guid.NewGuid(),19,Guid.Parse(Root),"Must roll back"));
        Assert.Equal("42501",denied.SqlState);
        Assert.Equal(before,await ProtectedRowsAsync());
        await using (var deployer=await postgres.OpenAsync())
        {
            await using (var settings=new NpgsqlCommand("SELECT set_config('hrm.group_storage_review_approved','yes',false), set_config('hrm.group_storage_runtime_role','group_storage_fixture_app',false)",deployer))
                await settings.ExecuteNonQueryAsync();
            await using var grant=new NpgsqlCommand(await File.ReadAllTextAsync(postgres.ScriptPath("04-runtime-grants.review.sql")),deployer);
            await grant.ExecuteNonQueryAsync();
        }
        var id=Guid.NewGuid();
        await InsertLegacyAsync(runtime,id,19,Guid.Parse(Root),"Created by runtime role");
        await using var check=new NpgsqlCommand("SELECT \"DisplayName\" FROM \"InternalMail\".\"InternalConversationGroups\" WHERE \"ConversationId\"=@id",runtime);
        check.Parameters.AddWithValue("id",id);
        Assert.Equal("Created by runtime role",await check.ExecuteScalarAsync());
    }

    [PostgresGroupStorageTheory]
    [InlineData(null)]
    [InlineData("public")]
    [InlineData("missing_group_storage_role")]
    public async Task GrantScriptRejectsImplicitPublicOrUnknownRoles(string? role)
    {
        await postgres.ApplyAsync();
        await using var deployer=await postgres.OpenAsync();
        await using (var settings=new NpgsqlCommand("SELECT set_config('hrm.group_storage_review_approved','yes',false),set_config('hrm.group_storage_runtime_role',@role,false)",deployer))
        {
            settings.Parameters.AddWithValue("role",role ?? "");
            await settings.ExecuteNonQueryAsync();
        }
        await using var command=new NpgsqlCommand(await File.ReadAllTextAsync(postgres.ScriptPath("04-runtime-grants.review.sql")),deployer);
        var error=await Assert.ThrowsAsync<PostgresException>(()=>command.ExecuteNonQueryAsync());
        Assert.Equal("P0001",error.SqlState);
    }
}
