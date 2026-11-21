using HRM.Application.Features.PLM.SampleRequests.Dtos;
using HRM.Application.Features.InternalMail.Queries.GetSampleRequestSummary;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.PLM.SampleRequests.Services.ConversationSummary;
using HRM.Application.Features.PLM.SampleRequests.Commands.SyncConversationSummaries;
using HRM.Application.Commons.Authorization;
using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData("LabUser")]
    [InlineData("SaleUser")]
    [InlineData("Developer")]
    public async Task CurrentSummaryRequiresAuthorizedGeneralRoot(string role)
    {
        var user = new User(role);
        var fixture = CreateStore(user, InternalMailRelatedType.ConversationTechnical, true);
        var sampleId = Guid.NewGuid();
        fixture.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        fixture.Root.RelatedId = sampleId;
        fixture.Root.Participants.Add(new() { EmployeeId = user.EmployeeId!.Value, IsActive = true });
        var target = new SummaryStoreSpy();
        var handler = new GetSampleRequestConversationSummaryQueryHandler(user, new InternalMailAreaAccessService(fixture.Db, user), target);
        Assert.NotNull(await handler.Handle(new(fixture.Root.InternalConversationId), default));
        Assert.Equal((user.CompanyId!.Value, sampleId), target.LastRead);
        target.LastRead = null;
        Assert.Null(await handler.Handle(new(fixture.Child.InternalConversationId), default));
        Assert.Null(target.LastRead);
        fixture.Root.CompanyId = Guid.NewGuid();
        Assert.Null(await handler.Handle(new(fixture.Root.InternalConversationId), default));
    }

    [Fact]
    public async Task SummaryReadDoesNotBypassMembershipAndDevMutationNeedsBothCapabilities()
    {
        var user = new User("SaleUser");
        var fixture = CreateStore(user, InternalMailRelatedType.Internal, false);
        fixture.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        fixture.Root.RelatedId = Guid.NewGuid();
        var target = new SummaryStoreSpy();
        Assert.Null(await new GetSampleRequestConversationSummaryQueryHandler(user,
            new InternalMailAreaAccessService(fixture.Db, user), target).Handle(new(fixture.Root.InternalConversationId), default));
        Assert.Null(target.LastRead);
        var denied = new SyncConversationSummariesCommandHandler(user, new SummaryPermissionSpy(false), target);
        Assert.Null(await denied.Handle(new(), default));
        Assert.Equal(0, target.Queued);
        var allowed = new SyncConversationSummariesCommandHandler(user, new SummaryPermissionSpy(true), target);
        Assert.Null(await allowed.Handle(new(Take: 101), default));
        Assert.NotNull(await allowed.Handle(new(Take: 100), default));
        Assert.Equal(1, target.Queued);
        user.IsAuthenticated = false;
        Assert.Null(await allowed.Handle(new(), default));
        Assert.Equal(1, target.Queued);
    }

    private sealed class SummaryPermissionSpy(bool retry) : ICurrentUserPermissionService
    {
        public bool HasPermission(string permission) => permission == ApplicationPermissions.DevAndQA.ViewWorkflowNotificationJobs ||
            permission == ApplicationPermissions.DevAndQA.RetryWorkflowNotificationJobs && retry;
    }
    private sealed class SummaryStoreSpy : ISampleRequestConversationSummaryStore
    {
        public (Guid Company, Guid Sample)? LastRead;
        public int Queued;
        public Task<ConversationSummaryDto> ReadAsync(Guid company, Guid sampleRequest, CancellationToken ct)
        { LastRead = (company, sampleRequest); return Task.FromResult(new ConversationSummaryDto("Synced", null, [])); }
        public Task<SummaryBackfillPage> EnqueuePageAsync(Guid company, Guid? cursor, int take, Guid? sampleRequest, CancellationToken ct)
        { Queued++; return Task.FromResult(new SummaryBackfillPage(1, null, false)); }
        public Task<SummarySyncStatus> StatusAsync(Guid company, CancellationToken ct) => Task.FromResult(new SummarySyncStatus(true, 0, 0, 0));
        public Task<bool> ProcessOneAsync(CancellationToken ct) => throw new NotSupportedException();
    }
}
