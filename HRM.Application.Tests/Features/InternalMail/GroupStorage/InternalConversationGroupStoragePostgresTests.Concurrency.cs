using Npgsql;
using Xunit;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    [PostgresGroupStorageTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConcurrentDefaultCreationUsesUniqueSlotAndTransactionOutcome(bool commitFirst)
    {
        await postgres.ApplyAsync();
        await using var first = await postgres.OpenAsync();
        await using var second = await postgres.OpenAsync();
        int secondPid;
        await using (var backend = new NpgsqlCommand("SELECT pg_backend_pid()",second))
            secondPid=(int)(await backend.ExecuteScalarAsync())!;
        await using var firstTransaction=await first.BeginTransactionAsync();
        await using var secondTransaction=await second.BeginTransactionAsync();
        var quotationRoot=Guid.Parse("20000000-0000-0000-0000-000000000002");
        await InsertLegacyAsync(first,Guid.NewGuid(),17,quotationRoot,"First",firstTransaction);
        var competingInsert=InsertLegacyAsync(second,Guid.NewGuid(),17,quotationRoot,"Second",secondTransaction);
        await WaitUntilLockBlockedAsync(secondPid);
        if (commitFirst)
        {
            await firstTransaction.CommitAsync();
            var error=await Assert.ThrowsAsync<PostgresException>(async()=>await competingInsert.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal("23505",error.SqlState);
            await secondTransaction.RollbackAsync();
        }
        else
        {
            await firstTransaction.RollbackAsync();
            await competingInsert.WaitAsync(TimeSpan.FromSeconds(10));
            await secondTransaction.CommitAsync();
        }
        Assert.Equal(1L,await ScalarAsync<long>("SELECT count(*) FROM \"InternalMail\".\"InternalConversationGroups\" WHERE \"RootConversationId\"='20000000-0000-0000-0000-000000000002' AND \"GroupType\"=1 AND \"IsActive\""));
        Assert.Equal(1L,await ScalarAsync<long>("SELECT count(*) FROM \"InternalMail\".\"InternalConversations\" WHERE \"RelatedId\"='20000000-0000-0000-0000-000000000002' AND \"RelatedType\"=17 AND \"IsActive\""));
    }

    [PostgresGroupStorageFact]
    public async Task GroupForeignKeysRejectPhysicalDeletionWithoutCascadingProtectedRows()
    {
        await postgres.ApplyAsync();
        var before=await ProtectedRowsAsync();
        var error=await Assert.ThrowsAsync<PostgresException>(()=>postgres.ExecuteAsync("DELETE FROM \"InternalMail\".\"InternalConversations\" WHERE \"InternalConversationId\"='30000000-0000-0000-0000-000000000003'"));
        Assert.Equal("23503",error.SqlState);
        Assert.Equal(before,await ProtectedRowsAsync());
    }

    [PostgresGroupStorageFact]
    public async Task LegacyStructureChangeCannotReparentExistingGroup()
    {
        await postgres.ApplyAsync();
        var before=await ProtectedRowsAsync();
        var error=await Assert.ThrowsAsync<PostgresException>(()=>postgres.ExecuteAsync("UPDATE \"InternalMail\".\"InternalConversations\" SET \"RelatedId\"='20000000-0000-0000-0000-000000000002' WHERE \"InternalConversationId\"='30000000-0000-0000-0000-000000000004'"));
        Assert.Equal("P0001",error.SqlState);
        Assert.Equal(before,await ProtectedRowsAsync());
    }

    private async Task WaitUntilLockBlockedAsync(int pid)
    {
        await using var observer=await postgres.OpenAsync();
        for(var attempt=0;attempt<100;attempt++)
        {
            await using var command=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE pid=@pid AND wait_event_type='Lock')",observer);
            command.Parameters.AddWithValue("pid",pid);
            if ((bool)(await command.ExecuteScalarAsync())!) return;
            await Task.Delay(50);
        }
        Assert.Fail("Competing writer was not observed waiting on the unique slot lock.");
    }
}
