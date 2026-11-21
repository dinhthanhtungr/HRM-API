using System.Reflection;
using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.SampleRequests.Commands.RecordQuotationFeedback;
using HRM.Application.Features.PLM.SampleRequests.Commands.PatchSampleRequest;
using HRM.Application.Features.PLM.SampleRequests.Commands.ConfirmCustomerPrice;
using HRM.Application.Features.PLM.SampleRequests.Commands.ApplyFlowAction;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using MediatR;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class RecordSampleQuotationFeedbackTests
{
    [Theory]
    [InlineData("Accepted", true, false)]
    [InlineData("Rejected", true, false)]
    [InlineData("NoResponse", true, false)]
    [InlineData("Accepted", false, false)]
    [InlineData("Rejected", false, false)]
    [InlineData("Accepted", true, true)]
    [InlineData("Rejected", true, true)]
    public async Task CombinedSaveUsesTrackedTokenAndRollsBackWhenFeedbackFails(string outcome, bool succeeds, bool databasePrecision)
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("SaleUser");
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        var record = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            ManagerBy = user.EmployeeId!.Value, IsActive = true, UpdatedDate = DateTime.Now.AddHours(-2) };
        store.Seed(nameof(db.SampleRequests), record);
        store.Seed(nameof(db.Employees), new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId.Value, IsActive = true });
        var sender = DispatchProxy.Create<ISender, Sender>();
        var proxy = (Sender)sender;
        proxy.Record = record;
        proxy.FeedbackSucceeds = succeeds;
        if (databasePrecision)
            proxy.AfterPatch = () => store.Seed(nameof(db.SampleRequests), new SampleRequest
            {
                SampleRequestId = record.SampleRequestId, CompanyId = record.CompanyId,
                UpdatedDate = new DateTime(record.UpdatedDate!.Value.Ticks / 10 * 10)
            });
        var (crm, crmStore) = ProductionOrderTestStore.CreateContext<ICRMWriteDbContext>();
        record.CustomerId = Guid.NewGuid();
        crmStore.Seed(nameof(crm.Customers), new Customer { CustomerId = record.CustomerId, CompanyId = record.CompanyId, IsActive = true });
        var handler = new RecordSampleQuotationFeedbackCommandHandler(db, user, new CurrentUserPermissionService(user),
            SampleTrialFeedbackCorrectionTests.Visibility.Create(user), sender, crm);
        var result = await handler.Handle(new() { SampleRequestId = record.SampleRequestId,
            ExpectedUpdatedDate = record.UpdatedDate, QuotedAt = DateTime.Today.AddDays(-1).AddHours(14).AddMinutes(32).AddSeconds(17),
            Feedback = new() { Outcome = outcome, OccurredAt = DateTime.Today, Reference = "BBG-test", CustomerFeedbackNote = "Customer response" } }, default);
        Assert.Equal(succeeds, result.Success);
        Assert.Equal(succeeds, store.Transaction.Committed);
        Assert.Equal(!succeeds, store.Transaction.RolledBack);
        Assert.Equal(2, proxy.Commands.Count);
        Assert.True(Assert.IsType<PatchSampleRequestCommand>(proxy.Commands[0]).DeferSaveChanges);
        Assert.Equal(new TimeSpan(14, 32, 17), record.RealPriceQuoteDate!.Value.TimeOfDay);
        if (outcome == "Accepted") Assert.Equal(record.UpdatedDate, Assert.IsType<ConfirmCustomerPriceCommand>(proxy.Commands[1]).ExpectedUpdatedDate);
        else
        {
            var action = Assert.IsType<ApplySampleFlowActionCommand>(proxy.Commands[1]);
            Assert.True(action.UseExistingTransaction);
            Assert.Equal(record.UpdatedDate, action.ExpectedUpdatedDate);
            Assert.Equal(outcome == "Rejected" ? "PriceRejected" : "PriceNoResponse", action.Action);
        }
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("owner")]
    [InlineData("company")]
    [InlineData("date")]
    [InlineData("outcome")]
    [InlineData("privateOnly")]
    public async Task InvalidOrUnauthorizedRequestsNeverDispatchWrites(string invalid)
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("SaleUser");
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        var record = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            ManagerBy = user.EmployeeId!.Value, IsActive = true, UpdatedDate = DateTime.Now.AddHours(-2) };
        store.Seed(nameof(db.SampleRequests), record);
        store.Seed(nameof(db.Employees), new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId.Value, IsActive = true });
        if (invalid == "owner") record.ManagerBy = Guid.NewGuid();
        if (invalid == "company") record.CompanyId = Guid.NewGuid();
        var sender = DispatchProxy.Create<ISender, Sender>();
        var (crm, _) = ProductionOrderTestStore.CreateContext<ICRMWriteDbContext>();
        var handler = new RecordSampleQuotationFeedbackCommandHandler(db, user, new CurrentUserPermissionService(user), SampleTrialFeedbackCorrectionTests.Visibility.Create(user), sender, crm);
        var result = await handler.Handle(new() { SampleRequestId = record.SampleRequestId,
            ExpectedUpdatedDate = invalid == "stale" ? null : record.UpdatedDate,
            QuotedAt = invalid == "date" ? DateTime.Today.AddDays(2) : DateTime.Today,
            Feedback = new() { Outcome = invalid == "outcome" ? "Complete" : "Accepted", OccurredAt = DateTime.Today,
                Note = "Private pricing note", CustomerFeedbackNote = invalid == "privateOnly" ? null : "Shared feedback" } }, default);
        Assert.False(result.Success);
        Assert.Empty(((Sender)sender).Commands);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public void ClientCannotOptOutOfTransactionOwnership()
    {
        var command = JsonSerializer.Deserialize<ApplySampleFlowActionCommand>("{\"UseExistingTransaction\":true}")!;
        Assert.False(command.UseExistingTransaction);
    }

    [Fact]
    public void OptionalPricingSectionIsOmittedWhenBlank()
    {
        Assert.Equal("Nội dung gửi Lab:\nKhách đồng ý", QuotationFeedbackInteraction.Content(new()
        { CustomerFeedbackNote = " Khách đồng ý ", Note = "   " }));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FeedbackCreatesOneLinkedCrmInteractionWithBothNoteSections(bool success)
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("SaleUser");
        var (db, _) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        var (crm, crmStore) = ProductionOrderTestStore.CreateContext<ICRMWriteDbContext>();
        var customer = new Customer { CustomerId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, IsActive = true };
        var record = new SampleRequest { SampleRequestId = Guid.NewGuid(), CustomerId = customer.CustomerId, ExternalId = "TP-test",
            CompanyId = user.CompanyId.Value, ManagerBy = user.EmployeeId!.Value, IsActive = true, UpdatedDate = DateTime.Today };
        crmStore.Seed(nameof(crm.Customers), customer);
        var store = (ProductionOrderTestStore)db;
        store.Seed(nameof(db.SampleRequests), record);
        store.Seed(nameof(db.Employees), new Employee { EmployeeId = user.EmployeeId.Value, CompanyId = user.CompanyId.Value, IsActive = true });
        var sender = DispatchProxy.Create<ISender, Sender>();
        ((Sender)sender).Record = record;
        ((Sender)sender).FeedbackSucceeds = success;
        var command = new RecordSampleQuotationFeedbackCommand { SampleRequestId = record.SampleRequestId, ExpectedUpdatedDate = record.UpdatedDate,
            QuotedAt = DateTime.Today, Feedback = new() { Outcome = "Accepted", OccurredAt = DateTime.Today, Note = " CRM price note ", CustomerFeedbackNote = "Lab shared note", Reference = "BBG-test" } };
        var handler = new RecordSampleQuotationFeedbackCommandHandler(db, user, new CurrentUserPermissionService(user), SampleTrialFeedbackCorrectionTests.Visibility.Create(user), sender, crm);
        Assert.Equal(success, (await handler.Handle(command, default)).Success);
        if (!success)
        {
            Assert.Empty(crmStore.Added);
            Assert.True(store.Transaction.RolledBack);
            return;
        }
        var interaction = Assert.Single(crmStore.Added.OfType<CustomerInteraction>());
        var reference = Assert.Single(crmStore.Added.OfType<CustomerInteractionReference>());
        Assert.Equal("Nội dung gửi Lab:\nLab shared note\n\nGhi chú trao đổi giá:\nCRM price note", interaction.Content);
        Assert.Equal(CustomerInteractionType.Quotation, interaction.InteractionType);
        Assert.Equal(record.ManagerBy, interaction.AssignedSaleEmployeeId);
        Assert.Equal(user.EmployeeId, interaction.CreatedBy);
        Assert.Equal(record.CompanyId, interaction.CompanyId);
        Assert.Equal(record.CustomerId, interaction.CustomerId);
        Assert.Equal(record.SampleRequestId, reference.ReferenceId);
        Assert.Equal(CustomerInteractionReferenceType.SampleRequest, reference.ReferenceType);
        Assert.Equal(interaction.Id, reference.InteractionId);
        Assert.Equal(DateTime.Today, customer.LastContactDate);
        Assert.True(store.Transaction.Committed);
        // Simulate the committed aggregate and a lost response: the original token is now stale.
        interaction.References.Add(reference);
        crmStore.Seed(nameof(crm.CustomerInteractions), interaction);
        var calls = ((Sender)sender).Commands.Count;
        Assert.True((await handler.Handle(command, default)).Success);
        Assert.Equal(calls, ((Sender)sender).Commands.Count);
        Assert.Single(crmStore.Added.OfType<CustomerInteraction>());
    }

    public class Sender : DispatchProxy
    {
        public SampleRequest Record = null!;
        public bool FeedbackSucceeds;
        public Action? AfterPatch;
        public List<object> Commands { get; } = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            var command = args![0]!;
            Commands.Add(command);
            if (command is PatchSampleRequestCommand patch)
            {
                Record.RealPriceQuoteDate = patch.RealPriceQuoteDate;
                Record.UpdatedDate = new DateTime(DateTime.Now.Ticks / 10 * 10 + 7);
                AfterPatch?.Invoke();
                return Task.FromResult(OperationResult<Guid>.Ok(Record.SampleRequestId));
            }
            var expected = command switch
            {
                ConfirmCustomerPriceCommand confirm => confirm.ExpectedUpdatedDate,
                ApplySampleFlowActionCommand action => action.ExpectedUpdatedDate,
                _ => Record.UpdatedDate
            };
            if (expected != Record.UpdatedDate)
                return Task.FromResult(OperationResult<Guid>.Fail("Sample request changed. Reload before saving."));
            return Task.FromResult(FeedbackSucceeds ? OperationResult<Guid>.Ok(Record.SampleRequestId) : OperationResult<Guid>.Fail("Existing feedback rule rejected the action"));
        }
    }
}
