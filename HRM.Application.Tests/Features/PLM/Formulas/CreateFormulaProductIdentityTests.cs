using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.PLM.Formulas.Commands.CreateFormula;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Application.Tests.Features.PLM.SampleRequests;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Tests.Features.PLM.Formulas;

public sealed class CreateFormulaProductIdentityTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", "Product")]
    [InlineData("   ", "Product")]
    [InlineData("BP51079", null)]
    [InlineData("BP51079", "")]
    [InlineData("BP51079", " \t ")]
    public async Task RejectsMissingPersistedIdentityBeforeAddingFormulaOrAllocatingCode(string? code, string? name)
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("LabUser");
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        var product = new Product { ProductId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, IsActive = true, ColourCode = code, Name = name };
        store.Seed(nameof(db.Products), product);
        // Null code service/version service proves the request stops before allocation/snapshot.
        var handler = new CreateFormulaCommandHandler(db, user, new FormulaWriteService(db, null!), null!);
        var result = await handler.Handle(new(new UpsertFormulaRequest { ProductId = product.ProductId, Name = "Client formula name is not product identity" }), CancellationToken.None);
        Assert.False(result.Success);
        Assert.Contains("Product colour code and product name", result.Message);
        Assert.Empty(store.Added);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task ValidPersistedIdentityStillCreatesFormulaAndSnapshot()
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("LabUser");
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        var product = new Product { ProductId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, IsActive = true, ColourCode = " BP51079 ", Name = " Product " };
        store.Seed(nameof(db.Products), product);
        store.Seed(nameof(db.Formulas), Array.Empty<Formula>());
        store.Seed(nameof(db.FormulaMaterials), Array.Empty<FormulaMaterial>());
        store.Seed(nameof(db.FormulaVersions), Array.Empty<FormulaVersion>());
        var handler = new CreateFormulaCommandHandler(db, user, new FormulaWriteService(db, null!),
            new FormulaVersionService(db, new KeyedMutationLock<Guid>()));
        var result = await handler.Handle(new(new UpsertFormulaRequest { ProductId = product.ProductId, ExternalId = "VU-TEST", Name = "F001" }), CancellationToken.None);
        Assert.True(result.Success, result.Message);
        Assert.Single(store.Added.OfType<Formula>());
        Assert.Single(store.Added.OfType<FormulaVersion>());
        Assert.Equal(1, store.Saves);
    }

    [Fact]
    public async Task CompleteIdentityDoesNotBypassCompanyOrActiveScope()
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("LabUser");
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        var product = new Product { ProductId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true, ColourCode = "BP51079", Name = "Product" };
        store.Seed(nameof(db.Products), product);
        var handler = new CreateFormulaCommandHandler(db, user, new FormulaWriteService(db, null!), null!);
        var command = new CreateFormulaCommand(new UpsertFormulaRequest { ProductId = product.ProductId });
        Assert.False((await handler.Handle(command, CancellationToken.None)).Success);
        product.CompanyId = user.CompanyId!.Value; product.IsActive = false;
        Assert.False((await handler.Handle(command, CancellationToken.None)).Success);
        Assert.Empty(store.Added);
    }
}
