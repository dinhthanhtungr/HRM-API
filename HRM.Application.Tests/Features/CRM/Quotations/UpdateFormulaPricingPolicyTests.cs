using System.Reflection;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.CRM.Quotations.Commands.UpdateFormulaPricingPolicy;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Infrastructure.DatabaseContext.Configurations.CustomerSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class UpdateFormulaPricingPolicyTests
{
    [Fact]
    public async Task AddingTier_RegistersInsertAndPreservesExistingTierIdentity()
    {
        var user = new IqcWorkflowTests.User { Roles = ["President"] };
        var policy = new FormulaPricingPolicy
        {
            FormulaPricingPolicyId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            Currency = "VND", Name = "Policy",
            Tiers = [new FormulaPricingPolicyTier
            {
                FormulaPricingPolicyTierId = Guid.NewGuid(), SortOrder = 0,
                QuantityRangeLabel = "Old", MinQuantity = 0, MaxQuantity = 100
            }]
        };
        var existingTier = policy.Tiers.Single();
        existingTier.FormulaPricingPolicyId = policy.FormulaPricingPolicyId;
        var existingId = existingTier.FormulaPricingPolicyTierId;
        var queries = IqcTestStore.CreateContext<ICRMWriteDbContext>();
        queries.Store.Seed(nameof(ICRMWriteDbContext.FormulaPricingPolicies), policy);
        using var tracking = new TierTrackingContext();
        tracking.Attach(existingTier);
        var db = DispatchProxy.Create<ICRMWriteDbContext, WriteProxy>();
        var proxy = (WriteProxy)(object)db;
        proxy.Queries = queries.Db;
        proxy.Tracking = tracking;
        var handler = new UpdateFormulaPricingPolicyCommandHandler(db, user, new Clock(), new KeyedMutationLock<Guid>());

        var result = await handler.Handle(new(policy.FormulaPricingPolicyId, new UpdateFormulaPricingPolicyRequest
        {
            Name = "Updated", RoundingIncrement = 1, EffectiveFrom = new(2026, 11, 12),
            Tiers = [
                new() { SortOrder = 0, QuantityRangeLabel = "0-100", MinQuantity = 0, MaxQuantity = 100, MaxInclusive = false },
                new() { SortOrder = 1, QuantityRangeLabel = "100+", MinQuantity = 100 }
            ]
        }), default);

        Assert.True(result.Success, result.Message);
        Assert.True(proxy.Saved);
        var added = Assert.Single(tracking.ChangeTracker.Entries<FormulaPricingPolicyTier>(), e => e.State == EntityState.Added);
        Assert.Same(policy.Tiers.Single(x => x.SortOrder == 1), added.Entity);
        Assert.Equal(policy.FormulaPricingPolicyId, added.Entity.FormulaPricingPolicyId);
        Assert.NotEqual(Guid.Empty, added.Entity.FormulaPricingPolicyTierId);
        Assert.Equal(existingId, existingTier.FormulaPricingPolicyTierId);
        Assert.Equal(EntityState.Modified, tracking.Entry(existingTier).State);
        Assert.Equal("0-100", existingTier.QuantityRangeLabel);
        Assert.Equal(FormulaPricingPolicyStatus.Published, policy.Status);
    }

    private sealed class Clock : IDateTimeProvider
    {
        public DateTime Now => new(2026, 11, 12);
    }

    // Uses the real EF state manager and production key configuration without opening a connection.
    public sealed class TierTrackingContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseNpgsql();
        protected override void OnModelCreating(ModelBuilder model)
        {
            model.ApplyConfiguration(new FormulaPricingPolicyTierConfiguration());
            model.Entity<FormulaPricingPolicyTier>().Ignore(x => x.FormulaPricingPolicy);
        }
    }

    public class WriteProxy : DispatchProxy
    {
        public ICRMWriteDbContext Queries { get; set; } = null!;
        public TierTrackingContext Tracking { get; set; } = null!;
        public bool Saved { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "get_FormulaPricingPolicyTiers")
                return Tracking.Set<FormulaPricingPolicyTier>();
            if (method.Name == nameof(ICRMWriteDbContext.SaveChangesAsync))
            {
                Tracking.ChangeTracker.DetectChanges();
                Assert.Single(Tracking.ChangeTracker.Entries<FormulaPricingPolicyTier>(), e => e.State == EntityState.Added);
                Saved = true;
                return Task.FromResult(1);
            }
            return method.Invoke(Queries, args);
        }
    }
}
