using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Formulas.Queries.GetFormulaLookup;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Tests.Features.PLM.Formulas;

public sealed class FormulaLookupScopeTests
{
    [Fact]
    public async Task MissingSampleRequestMustNotExpandToCompanyWideLookup()
    {
        var (db, store) = IqcTestStore.CreateContext<IPLMReadDbContext>();
        var company = Guid.NewGuid();
        store.Seed(nameof(db.Formulas), new Formula {
            FormulaId = Guid.NewGuid(), ProductId = Guid.NewGuid(),
            Product = new Product { CompanyId = company }, IsActive = true
        });
        var handler = new GetFormulaLookupQueryHandler(db, new User(company));
        var result = await handler.Handle(new GetFormulaLookupQuery {
            SampleRequestId = Guid.NewGuid(), SourceType = FormulaSource.FromVU
        }, default);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task UnscopedLookupStillReturnsOnlyCurrentCompanyActiveFormulas()
    {
        var (db, store) = IqcTestStore.CreateContext<IPLMReadDbContext>();
        var company = Guid.NewGuid();
        var expected = Guid.NewGuid();
        Formula Row(Guid id, Guid owner, bool active = true) => new() {
            FormulaId = id, Product = new Product { CompanyId = owner }, IsActive = active
        };
        store.Seed(nameof(db.Formulas), Row(expected, company),
            Row(Guid.NewGuid(), Guid.NewGuid()), Row(Guid.NewGuid(), company, false));
        var result = await new GetFormulaLookupQueryHandler(db, new User(company)).Handle(
            new GetFormulaLookupQuery { SourceType = FormulaSource.FromVU }, default);
        Assert.Equal(expected, Assert.Single(result.Items).FormulaId);
    }

    [Fact]
    public async Task SampleRequestOutsideCompanyMustNotRemoveContextFilter()
    {
        var (db, store) = IqcTestStore.CreateContext<IPLMReadDbContext>();
        var company = Guid.NewGuid();
        var sampleRequestId = Guid.NewGuid();
        store.Seed(nameof(db.SampleRequests), new SampleRequest {
            SampleRequestId = sampleRequestId, ProductId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(), IsActive = true
        });
        store.Seed(nameof(db.Formulas), new Formula {
            FormulaId = Guid.NewGuid(), Product = new Product { CompanyId = company }, IsActive = true
        });
        var result = await new GetFormulaLookupQueryHandler(db, new User(company)).Handle(
            new GetFormulaLookupQuery { SampleRequestId = sampleRequestId, SourceType = FormulaSource.FromVU }, default);
        Assert.Empty(result.Items);
    }

    private sealed record User(Guid Company) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId => Guid.Empty;
        public Guid? EmployeeId => Guid.Empty;
        public Guid? CompanyId => Company;
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [];
        public bool IsInRole(string role) => false;
    }
}
