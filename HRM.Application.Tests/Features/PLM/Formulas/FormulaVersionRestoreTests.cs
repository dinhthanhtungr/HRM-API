using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.PLM.Formulas.Commands.RestoreFormulaVersion;
using HRM.Application.Features.PLM.Formulas.Dtos.Versions;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Application.Tests.Features.PLM.SampleRequests;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.Notifications;

namespace HRM.Application.Tests.Features.PLM.Formulas;

public sealed class FormulaVersionRestoreTests
{
    [Theory]
    [InlineData("Approved")]
    [InlineData("SampleSent")]
    [InlineData("Completed")]
    [InlineData("Draft")]
    public async Task RestoresContentAsDraftWithoutReplayingApprovalOrDelivery(string historicalStatus)
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("LabUser");
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        var now = new DateTime(2026, 11, 1);
        var formula = new Formula { FormulaId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            Status = "Approved", UpdatedDate = now, CheckBy = user.EmployeeId, CheckDate = now,
            SentBy = user.EmployeeId, SentDate = now };
        var source = new FormulaVersion { FormulaId = formula.FormulaId, Formula = formula,
            VersionNo = 1, Status = historicalStatus, Name = "Historical content", Note = "Old note", TotalPrice = 123 };
        store.Seed(nameof(db.Formulas), formula);
        store.Seed(nameof(db.FormulaVersions), source);
        var handler = new RestoreFormulaVersionCommandHandler(db, user,
            new FormulaVersionService(db, new KeyedMutationLock<Guid>()));
        var result = await handler.Handle(new(formula.FormulaId, 1,
            new RestoreFormulaVersionRequest { ExpectedUpdatedDate = now }), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal("Draft", formula.Status);
        Assert.Equal(source.Name, formula.Name); Assert.Equal(source.Note, formula.Note); Assert.Equal(123m, formula.TotalPrice);
        Assert.Null(formula.CheckBy); Assert.Null(formula.CheckDate); Assert.Null(formula.SentBy); Assert.Null(formula.SentDate);
        Assert.Equal(historicalStatus, source.Status);
        Assert.Equal("Draft", Assert.Single(store.Added.OfType<FormulaVersion>()).Status);
        Assert.Empty(store.Added.OfType<SampleRequestWorkflowEvent>());
        Assert.Empty(store.Added.OfType<OutboxMessage>());
        Assert.Equal(1, store.Saves);
    }
}
