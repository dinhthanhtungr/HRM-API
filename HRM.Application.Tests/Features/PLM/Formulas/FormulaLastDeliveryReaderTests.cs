using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.Formulas;

public sealed class FormulaLastDeliveryReaderTests
{
    [Fact]
    public void LatestDeliveryIsOneScopedServerQueryWithBoundedCorrelatedLookup()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid(); var id = Guid.NewGuid();
        var sql = FormulaLastDeliveryReader.Query(db, company, [id]).ToQueryString();
        Assert.Contains(company.ToString(), sql); Assert.Contains(id.ToString(), sql);
        Assert.Contains("SentDate", sql); Assert.Contains("IsActive", sql); Assert.Contains("LIMIT 1", sql);
        Assert.Contains("ColorChip", sql); Assert.Contains("TrialSample", sql);
    }
    [Fact]
    public async Task UsesLatestActualSentTrialNotRequestedKgAndPreservesTenantAndActiveScope()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        var company = Guid.NewGuid(); var now = new DateTime(2026, 11, 10);
        var chip = new Formula { FormulaId = Guid.NewGuid(), CompanyId = company, IsActive = true, Status = "Approved" };
        var kg = new Formula { FormulaId = Guid.NewGuid(), CompanyId = company, IsActive = true };
        var foreign = new Formula { FormulaId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true };
        var request = new SampleRequest { CompanyId = company, IsActive = true, SampleQuantity = 100 };
        SampleRequestSampleTrial Trial(Formula f, decimal? quantity, int day, bool active = true, SampleRequest? scope = null)
            => new() { FormulaId = f.FormulaId, Formula = f, SampleRequest = scope ?? request,
                SampleRequestSampleTrialId = Guid.NewGuid(), DeliveredSampleQuantityKg = quantity,
                SentDate = now.AddDays(day), IsActive = active };
        var unsent = Trial(chip, 100, 5); unsent.SentDate = null;
        store.Seed(nameof(db.Formulas), chip, kg, foreign);
        store.Seed(nameof(db.SampleRequestSampleTrials), Trial(chip, 0, 0), Trial(kg, 0, -1), Trial(kg, 2, 0),
            Trial(chip, 5, 2, active: false), unsent, Trial(foreign, 0, 1),
            Trial(chip, 100, 4, scope: new SampleRequest { CompanyId = Guid.NewGuid(), IsActive = true }));
        var result = await FormulaLastDeliveryReader.ReadAsync(db, company, [chip.FormulaId, kg.FormulaId, foreign.FormulaId], default);
        Assert.Equal("ColorChip", result[chip.FormulaId]);
        Assert.Equal("TrialSample", result[kg.FormulaId]);
        Assert.False(result.ContainsKey(foreign.FormulaId));
        Assert.Equal(0, store.Saves); Assert.Empty(store.Added);
    }
}
