using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Commands.PatchSampleRequest;
using HRM.Application.Features.PLM.SampleRequests.Commands.RecordSampleRequestSampleTrialCustomerFeedback;
using HRM.Domain.Enums.SampleRequests;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestCancellationScopeTests
{
    [Fact]
    public async Task ResponsibleSaleCannotCancelCompletedRequest()
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("SaleUser");
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            ManagerBy = user.EmployeeId!.Value, CustomerId = Guid.NewGuid(), Status = "Completed", IsActive = true };
        store.Seed(nameof(db.SampleRequests), request);
        store.Seed(nameof(db.Customers), new Customer { CustomerId = request.CustomerId, CompanyId = user.CompanyId.Value,
            IsActive = true, CurrentSaleId = user.EmployeeId });
        var handler = new PatchSampleRequestCommandHandler(db, user,
            SampleTrialFeedbackCorrectionTests.Visibility.Create(user), null!, null!, null!, new CurrentUserPermissionService(user));
        var result = await handler.Handle(new() { SampleRequestId = request.SampleRequestId, Status = "Cancelled",
            CancellationReason = "Mistaken cancellation" }, default);
        Assert.False(result.Success);
        Assert.Equal("Completed", request.Status);
        Assert.Empty(store.Added);
        Assert.Equal(0, store.Saves);
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \r\n ")]
    public async Task CancelledTrialFeedbackRejectsMissingReasonBeforeAccessingStorage(string? reason)
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("SaleUser");
        var handler = new RecordSampleRequestSampleTrialCustomerFeedbackCommandHandler(null!, user, null!, null!, null!, new CurrentUserPermissionService(user));
        var result = await handler.Handle(new() { SampleRequestId = Guid.NewGuid(), Status = SampleTrialStatus.Cancelled,
            CustomerReplyStatus = "Cancelled", CustomerReplyNote = reason }, default);
        Assert.False(result.Success);
        Assert.Contains("cancellation reason", result.Message);
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \r\n")]
    [InlineData("too-long")]
    public async Task OwnerCannotCancelWithoutAValidReason(string? reason)
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("SaleUser");
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            ManagerBy = user.EmployeeId!.Value, Status = "InProgress", IsActive = true };
        store.Seed(nameof(db.SampleRequests), request);
        request.CustomerId = Guid.NewGuid();
        store.Seed(nameof(db.Customers), new Customer { CustomerId = request.CustomerId, CompanyId = user.CompanyId.Value,
            IsActive = true, CurrentSaleId = user.EmployeeId });
        var handler = new PatchSampleRequestCommandHandler(db, user,
            SampleTrialFeedbackCorrectionTests.Visibility.Create(user), null!, null!, null!, new CurrentUserPermissionService(user));
        var result = await handler.Handle(new() { SampleRequestId = request.SampleRequestId, Status = "Cancelled",
            CancellationReason = reason == "too-long" ? new string('a', 2001) : reason }, default);
        Assert.False(result.Success);
        Assert.Contains("CancellationReason", result.Message);
        Assert.Equal("InProgress", request.Status);
        Assert.Empty(store.Added);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task CancellationPersistsTrimmedReasonWithoutOverwritingSaleNotes()
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("SaleUser");
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            ManagerBy = Guid.NewGuid(), CustomerId = Guid.NewGuid(), Status = "InProgress", IsActive = true, SaleComment = "Existing note" };
        store.Seed(nameof(db.SampleRequests), request);
        store.Seed(nameof(db.Customers), new Customer { CustomerId = request.CustomerId, CompanyId = user.CompanyId.Value,
            IsActive = true, CustomerAssignments = [new CustomerAssignment { CompanyId = user.CompanyId.Value,
                CustomerId = request.CustomerId, EmployeeId = user.EmployeeId!.Value, IsActive = true }] });
        var handler = new PatchSampleRequestCommandHandler(db, user,
            SampleTrialFeedbackCorrectionTests.Visibility.Create(user), null!, null!, null!, new CurrentUserPermissionService(user));
        var result = await handler.Handle(new() { SampleRequestId = request.SampleRequestId, Status = "Cancelled",
            CancellationReason = "  Customer stopped this project  " }, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal("Cancelled", request.Status);
        Assert.Equal("Existing note", request.SaleComment);
        var history = Assert.Single(store.Added.OfType<SampleRequestWorkflowEvent>());
        Assert.Equal("SampleRequestCancelled", history.EventType);
        Assert.Equal("Customer stopped this project", history.Note);
        Assert.Equal(user.EmployeeId, history.RecordedByEmployeeId);
    }

    [Fact]
    public async Task RequestOwnerCannotCancelAfterCustomerWasAssignedToAnotherSale()
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("SaleUser");
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            ManagerBy = user.EmployeeId!.Value, CustomerId = Guid.NewGuid(), Status = "InProgress", IsActive = true };
        store.Seed(nameof(db.SampleRequests), request);
        store.Seed(nameof(db.Customers), new Customer { CustomerId = request.CustomerId, CompanyId = user.CompanyId.Value,
            IsActive = true, CurrentSaleId = user.EmployeeId, CustomerAssignments = [new CustomerAssignment {
                CompanyId = user.CompanyId.Value, CustomerId = request.CustomerId, EmployeeId = Guid.NewGuid(), IsActive = true }] });
        var handler = new PatchSampleRequestCommandHandler(db, user,
            SampleTrialFeedbackCorrectionTests.Visibility.Create(user), null!, null!, null!, new CurrentUserPermissionService(user));
        var result = await handler.Handle(new() { SampleRequestId = request.SampleRequestId, Status = "Cancelled",
            CancellationReason = "Project ended" }, default);
        Assert.False(result.Success);
        Assert.Equal("InProgress", request.Status);
        Assert.Empty(store.Added);
        Assert.Equal(0, store.Saves);
    }

    [Theory]
    [InlineData("SaleUser", "InProgress")]
    [InlineData("SaleUser", "SampleSent")]
    [InlineData("SaleAdmin", "InProgress")]
    [InlineData("SaleAdmin", "SampleSent")]
    public async Task BroadReadCannotCancelForAnUnmanagedOwner(string role, string status)
    {
        var user = new SampleTrialFeedbackCorrectionTests.User(role);
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            ManagerBy = Guid.NewGuid(), Status = status, IsActive = true };
        store.Seed(nameof(db.SampleRequests), request);
        var handler = new PatchSampleRequestCommandHandler(db, user,
            SampleTrialFeedbackCorrectionTests.Visibility.Create(user), null!, null!, null!, new CurrentUserPermissionService(user));
        var result = await handler.Handle(new() { SampleRequestId = request.SampleRequestId, Status = "Cancelled" }, default);
        Assert.False(result.Success);
        Assert.Contains("responsible Sale", result.Message);
        Assert.Equal(status, request.Status);
        Assert.Equal(0, store.Saves);
    }
}
