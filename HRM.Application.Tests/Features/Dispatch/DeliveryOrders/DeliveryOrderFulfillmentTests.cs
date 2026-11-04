using System.Data;
using System.Data.Common;
using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Features.Dispatch.DeliveryOrders.Services;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HRM.Application.Tests.Features.Dispatch.DeliveryOrders;

public sealed class DeliveryOrderFulfillmentTests
{
    [Fact]
    public async Task Sync_TranslatesAllPostgresUpdates_WithoutOpeningDatabase()
    {
        var interceptor = new CaptureCommands(Guid.NewGuid());
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql()
            .AddInterceptors(interceptor).Options);

        await new DeliveryOrderFulfillmentService((IDispatchWriteDbContext)db)
            .SyncAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), default);

        Assert.Equal(4, interceptor.Updates.Count);
        Assert.All(interceptor.Updates, sql => Assert.Contains("CompanyId", sql));
        var quantityUpdate = interceptor.Updates[1];
        Assert.Contains("sum(", quantityUpdate.ToLowerInvariant());
        Assert.Contains("RealQuantity", quantityUpdate);
        Assert.Contains("cancelled", quantityUpdate);
        Assert.Contains("IsAttach", quantityUpdate);
        Assert.DoesNotContain("HasPrinted", quantityUpdate);
        Assert.Contains("Delivered", interceptor.Updates[2]);
        Assert.Contains("Processing", interceptor.Updates[3]);
        Assert.Contains("UpdatedBy", interceptor.Updates[3]);
    }

    // Suppress connection and execution, while exercising EF's actual PostgreSQL translator.
    private sealed class CaptureCommands(Guid poId) : DbConnectionInterceptor, IDbCommandInterceptor
    {
        public List<string> Updates { get; } = [];

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(InterceptionResult.Suppress());

        public ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            var table = new DataTable();
            table.Columns.Add("MerchandiseOrderId", typeof(Guid));
            table.Rows.Add(poId);
            return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(table.CreateDataReader()));
        }

        public ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Updates.Add(command.CommandText);
            return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(1));
        }
    }
}
