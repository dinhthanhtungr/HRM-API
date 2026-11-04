using System.Text.Json;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Reflection;
using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Application.Features.CRM.Quotations.Commands.CreateQuotation;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class QuotationDatabaseFactAttribute : FactAttribute
{
    public QuotationDatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("QUOTATION_DB_VERIFY_CONFIG")))
            Skip = "Opt-in verification against an existing local development database; always rolls back.";
    }
}

public sealed class QuotationSavePostgresTests
{
    [QuotationDatabaseFact]
    public async Task RealPostgresPersistsZeroAndReboundReferenceWithinRollbackTransaction()
    {
        var path = Environment.GetEnvironmentVariable("QUOTATION_DB_VERIFY_CONFIG")!;
        using var config = JsonDocument.Parse(await File.ReadAllTextAsync(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var connections = config.RootElement.EnumerateObject().Single(x => string.Equals(x.Name, "ConnectionStrings", StringComparison.OrdinalIgnoreCase)).Value;
        var connection = connections.EnumerateObject().Single(x => string.Equals(x.Name, "AppDbConnectionString", StringComparison.OrdinalIgnoreCase)).Value.GetString()!;
        var builder = new NpgsqlConnectionStringBuilder(connection);
        Assert.True(builder.Host is "localhost" or "127.0.0.1", "This verification only accepts a local development DB.");
        Assert.Contains("dev", builder.Database!, StringComparison.OrdinalIgnoreCase);
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var template = await db.Quotations.AsNoTracking().Where(x => x.IsActive && x.Lines.Any(y => y.IsActive && y.ProductNavigation.IsActive))
            .Select(x => new { x.CompanyId, x.CustomerId, x.SaleEmployeeId,
                ProductId = x.Lines.Where(y => y.IsActive && y.ProductNavigation.IsActive).Select(y => y.ProductId).First() })
            .FirstAsync();
        var templateProduct = await db.Products.AsNoTracking().SingleAsync(x => x.ProductId == template.ProductId && x.CompanyId == template.CompanyId);
        var testId = Guid.NewGuid(); var code = "TEST-Q-" + testId.ToString("N")[..12];
        Guid? createdId = null;
        Guid? retainedId = null;
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var product = new Product();
            db.Entry(product).CurrentValues.SetValues(templateProduct);
            product.ProductId = Guid.NewGuid(); product.Code = code; product.ColourCode = code; product.Name = "Quotation rollback verification";
            db.Products.Add(product);
            var old = new ProductPricingVersion { ProductPricingVersionId = Guid.NewGuid(), CompanyId = template.CompanyId,
                ProductId = product.ProductId, Currency = "VND", Version = 1, Status = ProductPricingStatus.Superseded,
                StandardSellingPrice = 100, MaterialCostSnapshot = 50, ManufacturingCost = 10, ProfitMarginRate = 40,
                CreatedBy = template.SaleEmployeeId, CreatedDate = DateTime.Now,
                PriceTiers = [new() { QuantityRangeLabel = "all", UnitPrice = 100 }] };
            var current = new ProductPricingVersion { ProductPricingVersionId = Guid.NewGuid(), CompanyId = template.CompanyId,
                ProductId = product.ProductId, Currency = "VND", Version = 2, Status = ProductPricingStatus.Approved,
                StandardSellingPrice = 120, MaterialCostSnapshot = 50, ManufacturingCost = 10, ProfitMarginRate = 50,
                CreatedBy = template.SaleEmployeeId, CreatedDate = DateTime.Now,
                PriceTiers = [new() { QuantityRangeLabel = "all", UnitPrice = 120 }] };
            db.ProductPricingVersions.AddRange(old, current);
            var quote = new Quotation { QuotationId = testId, ExternalId = code, CompanyId = template.CompanyId,
                CustomerId = template.CustomerId, SaleEmployeeId = template.SaleEmployeeId, Status = QuotationStatus.Draft,
                Currency = "VND", ExchangeRate = 1, CreatedBy = template.SaleEmployeeId, CreatedDate = DateTime.Now,
                QuotationDate = DateTime.Today, UpdatedDate = DateTime.Now,
                Lines = [new() { QuotationLineId = Guid.NewGuid(), ProductId = product.ProductId, Quantity = 10, Unit = "Kg",
                    ProductExternalIdSnapshot = code, ProductNameSnapshot = product.Name!, ProductPricingVersionId = old.ProductPricingVersionId,
                    UnitPrice = 77, PriceMode = QuotationLinePriceMode.ApprovedPricingEditable,
                    PriceTiers = [new() { QuantityRangeLabel = "all" }] }] };
            quote.Lines.Single().PriceTiers.Single().SetPrices(0, 0);
            db.Quotations.Add(quote);
            await db.SaveChangesAsync();
            var service = new QuotationStandardReferenceSaveService(db);
            var change = Assert.Single(await service.PrepareAsync(quote, new(), default));
            Assert.True(change.RequiresAcceptance);
            Assert.Equal(old.ProductPricingVersionId, quote.Lines.Single().ProductPricingVersionId);
            var accepted = new SaveQuotationRequest { AcceptedStandardPriceChanges = new Dictionary<Guid, string>
                { [change.QuotationLineId] = change.AcceptanceToken! } };
            await service.PrepareAsync(quote, accepted, default);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var persisted = await db.Quotations.AsNoTracking().Include(x => x.Lines).ThenInclude(x => x.PriceTiers)
                .SingleAsync(x => x.QuotationId == testId);
            Assert.Equal(current.ProductPricingVersionId, persisted.Lines.Single().ProductPricingVersionId);
            Assert.Equal(77, persisted.Lines.Single().UnitPrice);
            Assert.Equal(0, persisted.Lines.Single().PriceTiers.Single().UnitPrice);
            Assert.Equal(0, persisted.Lines.Single().PriceTiers.Single().CommissionAmount);

            // Exercise the retained-reference SQL predicate and write a separate TEST quotation.
            current.Status = ProductPricingStatus.Superseded;
            db.ProductPricingVersions.Attach(current);
            db.Entry(current).Property(x => x.Status).IsModified = true;
            await db.SaveChangesAsync();
            var incoming = new QuotationLineRequest
            {
                QuotationLineId = persisted.Lines.Single().QuotationLineId, ProductId = product.ProductId,
                ProductPricingVersionId = current.ProductPricingVersionId,
                PriceMode = QuotationLinePriceMode.ApprovedPricingEditable, Quantity = 10, Unit = "Kg", UnitPrice = 77,
                PriceTiers = [new() { QuantityRangeLabel = "all", UnitPrice = 0 }]
            };
            var unavailable = Assert.Single(await service.PrepareAsync(persisted, new() { Lines = [incoming] }, default));
            Assert.True(unavailable.IsUnavailable);
            retainedId = Guid.NewGuid();
            var built = await new QuotationLineBuilder(db).BuildAsync(retainedId.Value, template.CompanyId,
                template.CustomerId, "VND", 1, [incoming], default, new HashSet<Guid> { unavailable.PreviousVersionId });
            Assert.True(built.Success, built.Message);
            db.Quotations.Add(new Quotation
            {
                QuotationId = retainedId.Value, ExternalId = code + "-R", CompanyId = template.CompanyId,
                CustomerId = template.CustomerId, SaleEmployeeId = template.SaleEmployeeId, Currency = "VND", ExchangeRate = 1,
                CreatedBy = template.SaleEmployeeId, CreatedDate = DateTime.Now, QuotationDate = DateTime.Today,
                Lines = built.Data!.ToList()
            });
            await db.SaveChangesAsync();
            var retained = await db.QuotationLines.AsNoTracking().SingleAsync(x => x.QuotationId == retainedId);
            Assert.Equal(current.ProductPricingVersionId, retained.ProductPricingVersionId);
            Assert.Equal(77, retained.UnitPrice);

            var generated = 0;
            var visibility = Proxy<ICustomerVisibilityService>((name, args) => name switch
            {
                "BuildScopeAsync" => Task.FromResult(new ViewerScope(template.CompanyId, template.SaleEmployeeId, true, true, new HashSet<Guid>(), new HashSet<Guid>(), DateTime.Now)),
                "ApplyCustomerVisibility" => args![0],
                _ => throw new NotSupportedException(name)
            });
            var clock = Proxy<IDateTimeProvider>((_, _) => DateTime.Now);
            var ids = Proxy<IExternalIdService>((_, _) => { generated++; return Task.FromResult(code + "-C"); });
            var handler = new CreateQuotationCommandHandler(db, db, visibility, ids, clock, new QuotationLineBuilder(db), new KeyedMutationLock<Guid>());
            var create = new CreateQuotationRequest
            {
                IdempotencyKey = Guid.NewGuid(), CustomerId = template.CustomerId, Currency = "VND", ExchangeRate = 1,
                QuotationDate = DateTime.Today, Note = "TEST rollback only",
                Lines = [new() { ProductId = product.ProductId, Quantity = 10, Unit = "Kg", UnitPrice = 0,
                    PriceMode = QuotationLinePriceMode.ManualAuthorized,
                    PriceTiers = [new() { QuantityRangeLabel = "all", UnitPrice = 0 }] }]
            };
            // A scoped DbContext must not run concurrent queries. Concurrency is covered by the orchestration test;
            // this real-DB transaction verifies persistence and replay on the same connection before rollback.
            var firstCreate = await handler.Handle(new(create), default);
            var replay = await handler.Handle(new(create), default);
            Assert.True(firstCreate.Success, firstCreate.Message);
            Assert.True(replay.Success, replay.Message);
            createdId = firstCreate.Data!.QuotationId;
            Assert.Equal(createdId, replay.Data!.QuotationId);
            Assert.Equal(1, generated);
            Assert.Equal(1, await db.Quotations.CountAsync(x => x.QuotationId == createdId));
            Assert.Equal(0, (await db.Quotations.AsNoTracking().SingleAsync(x => x.QuotationId == createdId)).TotalAmount);
        }
        finally { await transaction.RollbackAsync(); }
        db.ChangeTracker.Clear();
        Assert.False(await db.Quotations.AsNoTracking().AnyAsync(x => x.QuotationId == testId));
        Assert.False(await db.Quotations.AsNoTracking().AnyAsync(x => x.QuotationId == createdId || x.QuotationId == retainedId));
        Assert.False(await db.Products.AsNoTracking().AnyAsync(x => x.Code == code));
    }

    private static T Proxy<T>(Func<string, object?[]?, object?> call) where T : class
    {
        var proxy = DispatchProxy.Create<T, DatabaseStub>();
        ((DatabaseStub)(object)proxy).Call = call;
        return proxy;
    }
    public class DatabaseStub : DispatchProxy
    {
        public Func<string, object?[]?, object?> Call { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Call(targetMethod!.Name, args);
    }
}
