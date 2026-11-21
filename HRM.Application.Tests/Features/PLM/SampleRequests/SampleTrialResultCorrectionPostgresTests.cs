using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Commands.CorrectSampleTrialFeedback;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleTrialFeedbackRevisions;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleTrialResultCorrectionPostgresTests(IsolatedGroupStoragePostgres postgres)
    : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task CorrectsBothDeliveryKindsAtomicallyAndRejectsProgressionStaleTokensAndMissingReason()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        foreach (var type in new[] { typeof(SampleRequest), typeof(Employee), typeof(AuditLog), typeof(Customer),
                     typeof(Formula), typeof(SampleRequestSampleTrial), typeof(SampleRequestWorkflowEvent) })
        {
            var entity = db.Model.FindEntityType(type)!;
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            var columns = entity.GetProperties().Select(p =>
            {
                var defaultSql = p.TryGetDefaultValue(out var value) && value is not null
                    ? " DEFAULT " + p.GetRelationalTypeMapping().GenerateSqlLiteral(value) : "";
                return $"\"{p.GetColumnName(table)}\" {p.GetRelationalTypeMapping().StoreType} NULL{defaultSql}";
            });
            var key = entity.FindPrimaryKey()!.Properties.Single().GetColumnName(table);
            // Trusted EF metadata, exclusively in the disposable local test cluster.
            await using var create = new NpgsqlCommand($"CREATE SCHEMA IF NOT EXISTS \"{table.Schema}\"; CREATE TABLE \"{table.Schema}\".\"{table.Name}\" ({string.Join(',', columns)}, PRIMARY KEY (\"{key}\"));", connection);
            await create.ExecuteNonQueryAsync();
        }
        var user = new SampleTrialFeedbackCorrectionTests.User("SaleUser");
        var permissions = new CurrentUserPermissionService(user);
        var visibility = SampleTrialFeedbackCorrectionTests.Visibility.Create(user);
        db.Employees.Add(new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId!.Value, IsActive = true,
            ExternalId = "SALE_TEST", FullName = "Sale test" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var baseline = new DateTime(2026, 11, 10, 10, 0, 0);
        var clock = new Clock { Now = baseline.AddMinutes(1) };
        foreach (var kg in new[] { 0m, 2m })
        foreach (var previous in new[] { SampleTrialStatus.Failed, SampleTrialStatus.Approved })
        {
            var next = previous == SampleTrialStatus.Failed ? SampleTrialStatus.Approved : SampleTrialStatus.Failed;
            var product = Guid.NewGuid();
            var customer = new Customer { CustomerId = Guid.NewGuid(), CompanyId = user.CompanyId.Value, ExternalId = "KH_TEST", IsActive = true };
            var formula = new Formula { FormulaId = Guid.NewGuid(), CompanyId = user.CompanyId.Value, ProductId = product,
                UpdatedDate = baseline, Status = SampleTrialResultCorrection.States(kg == 0 ? SampleDeliveryKind.ColorChip : SampleDeliveryKind.TrialSample, previous).Formula,
                PresidentPrice = 12345, IsSelect = false };
            var parent = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = user.CompanyId.Value,
                ProductId = product, CustomerId = customer.CustomerId, ManagerBy = user.EmployeeId.Value, IsActive = true,
                CreatedDate = new(2026, 11, 1), UpdatedDate = baseline, SampleQuantity = (double)kg, InfoType = "Thẻ màu (1)",
                Status = previous == SampleTrialStatus.Failed ? "InProgress" : "SampleSent" };
            var trial = new SampleRequestSampleTrial { SampleRequestSampleTrialId = Guid.NewGuid(), SampleRequestId = parent.SampleRequestId,
                FormulaId = formula.FormulaId, TrialNo = 1, Status = previous, DeliveredSampleQuantityKg = kg, SentDate = baseline.AddDays(-1),
                CustomerReplyDate = baseline, CustomerReplyNote = "Original", UpdatedDate = baseline };
            db.Customers.Add(customer); db.Formulas.Add(formula); db.SampleRequests.Add(parent); db.SampleRequestSampleTrials.Add(trial);
            db.SampleRequestWorkflowEvents.Add(SampleRequestWorkflowEvents.Create(parent.SampleRequestId, parent.CompanyId,
                user.EmployeeId.Value, baseline, SampleRequestWorkflowEvents.FeedbackRecorded, baseline, "Original", formula.FormulaId, trial.SampleRequestSampleTrialId));
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var handler = new CorrectSampleTrialFeedbackCommandHandler(db, user, permissions, visibility, clock);
            var command = new CorrectSampleTrialFeedbackCommand { SampleRequestId = parent.SampleRequestId, TrialId = trial.SampleRequestSampleTrialId,
                CustomerReplyDate = baseline, CustomerReplyNote = "Corrected", Status = next,
                ExpectedRequestUpdatedDate = baseline, ExpectedTrialUpdatedDate = baseline, ExpectedFormulaUpdatedDate = baseline };
            Assert.False((await handler.Handle(command, default)).Success); // mandatory reason
            command.CorrectionReason = "Selected the wrong result";
            command.ExpectedFormulaUpdatedDate = baseline.AddSeconds(-1);
            Assert.False((await handler.Handle(command, default)).Success);
            command.ExpectedFormulaUpdatedDate = baseline;
            command.Status = SampleTrialStatus.Cancelled;
            Assert.False((await handler.Handle(command, default)).Success);
            command.Status = next;
            var result = await handler.Handle(command, default);
            Assert.True(result.Success, result.Message);
            var actual = await db.SampleRequestSampleTrials.AsNoTracking().SingleAsync(x => x.SampleRequestSampleTrialId == trial.SampleRequestSampleTrialId);
            var expected = SampleTrialResultCorrection.States(trial.DeliveryKind, next);
            Assert.Equal(next, actual.Status);
            Assert.Equal(next == SampleTrialStatus.Approved ? "APPROVED" : "FAIL", actual.CustomerReplyStatus);
            Assert.Equal(user.EmployeeId, actual.CustomerReplyByEmployeeId);
            Assert.Equal(expected.Request, await db.SampleRequests.Where(x => x.SampleRequestId == parent.SampleRequestId).Select(x => x.Status).SingleAsync());
            var actualFormula = await db.Formulas.AsNoTracking().SingleAsync(x => x.FormulaId == formula.FormulaId);
            Assert.Equal(expected.Formula, actualFormula.Status);
            Assert.False(actualFormula.IsSelect); Assert.Equal(12345m, actualFormula.PresidentPrice);
            var actualParent = await db.SampleRequests.AsNoTracking().SingleAsync(x => x.SampleRequestId == parent.SampleRequestId);
            var priceAccepted = SampleRequestWorkflowEvents.Create(parent.SampleRequestId, parent.CompanyId, user.EmployeeId.Value,
                baseline.AddDays(-1), SampleRequestWorkflowEvents.PriceAccepted, formulaId: formula.FormulaId);
            var decision = SampleFlowStateReader.ResolveDecision(actualParent, actual, actualFormula, [priceAccepted], true, true, kg > 0, null, [actual]);
            Assert.Equal(next == SampleTrialStatus.Failed ? "Lab" : "Sale", decision.Role);
            Assert.Equal(next == SampleTrialStatus.Failed ? "ReworkFormula" : kg > 0 ? "CompleteRequest" : "ChooseAfterChip", decision.Code);
            var history = await new GetSampleTrialFeedbackRevisionsQueryHandler(db, user, permissions, visibility)
                .Handle(new(parent.SampleRequestId, trial.SampleRequestSampleTrialId), default);
            var revision = Assert.Single(history!.History.Items);
            Assert.Equal(previous, revision.OldStatus); Assert.Equal(next, revision.NewStatus);
            Assert.Equal(command.CorrectionReason, revision.CorrectionReason);
            Assert.True(history.CanEditResult);
            Assert.False((await handler.Handle(command, default)).Success); // stale repeat cannot add history
            command.Status = previous; command.ExpectedRequestUpdatedDate = clock.Now;
            command.ExpectedTrialUpdatedDate = clock.Now; command.ExpectedFormulaUpdatedDate = clock.Now;
            await db.Formulas.Where(x => x.FormulaId == formula.FormulaId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.UpdatedDate, clock.Now.AddSeconds(1)));
            Assert.False((await handler.Handle(command, default)).Success); // Lab continued after dialog opened
            Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.RecordId == trial.SampleRequestSampleTrialId));
            Assert.Equal(clock.Now, await db.SampleRequests.Where(x => x.SampleRequestId == parent.SampleRequestId).Select(x => x.UpdatedDate).SingleAsync());
        }
    }
    private sealed class Clock : IDateTimeProvider { public DateTime Now { get; set; } }
}
