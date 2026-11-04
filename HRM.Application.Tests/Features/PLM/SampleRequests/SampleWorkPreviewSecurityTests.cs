using System.Reflection;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Commands.UpdateSampleWorkSchedule;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.PreviewSampleWork;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.HrSchema;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleWorkPreviewSecurityTests
{
    [Fact]
    public void ControllerSourceContractIsDeveloperAuthorizedAndGetOnly()
    {
        // This guards the API shape; handler tests below exercise authorization independently.
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "HRM.Api")))
            root = root.Parent;
        Assert.NotNull(root);
        var source = File.ReadAllText(Path.Combine(root.FullName, "HRM.Api", "Controllers", "PLM", "SampleWorkPreviewController.cs"));
        Assert.Contains("[Authorize(Roles = ApplicationRoles.Developer)]", source);
        Assert.Contains("[HttpGet(\"options\")]", source);
        Assert.Contains("[HttpGet]", source);
        Assert.DoesNotContain("[HttpPost", source);
        Assert.DoesNotContain("[HttpPatch", source);
        Assert.DoesNotContain("[HttpPut", source);
        Assert.DoesNotContain("[HttpDelete", source);
    }

    [Theory]
    [InlineData("LabUser", true)]
    [InlineData("LabAdmin", true)]
    [InlineData("SaleUser", true)]
    [InlineData("Admin", true)]
    [InlineData("Developer", false)]
    public async Task PreviewAndOptionsRejectNonDeveloperBeforeStorageOrNestedScopes(string role, bool authenticated)
    {
        var actor = new SampleTrialFeedbackCorrectionTests.User(role, authenticated);
        var scopes = new PreviewScopes();
        Assert.Null(await new GetSampleWorkPreviewOptionsQueryHandler(null!, actor).Handle(new(), default));
        Assert.Null(await new GetSampleWorkPreviewQueryHandler(null!, actor, null!, scopes)
            .Handle(new() { EmployeeId = Guid.NewGuid() }, default));
        Assert.Equal(0, scopes.Created);
    }

    [Fact]
    public async Task OptionsReturnOnlyActiveEmployeesInActorCompanyAndFixedPersonas()
    {
        var actor = new SampleTrialFeedbackCorrectionTests.User("Developer");
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        var actorEmployee = Employee(actor.CompanyId!.Value, actor.EmployeeId!.Value, true, "Developer");
        var target = Employee(actor.CompanyId.Value, Guid.NewGuid(), true, "Target");
        store.Seed(nameof(db.Employees), actorEmployee, target,
            Employee(actor.CompanyId.Value, Guid.NewGuid(), false, "Inactive"),
            Employee(Guid.NewGuid(), Guid.NewGuid(), true, "Other company"));
        var result = await new GetSampleWorkPreviewOptionsQueryHandler(db, actor).Handle(new(), default);
        Assert.NotNull(result);
        Assert.Equal(new[] { actorEmployee.EmployeeId, target.EmployeeId }.Order(), result.Employees.Select(x => x.EmployeeId).Order());
        Assert.Equal(new[] { "LabUser", "SaleUser", "LabAdmin" }, result.Personas);
        Assert.Equal(0, store.Saves);
        Assert.Empty(store.Added);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InactiveActorOrForeignTargetNeverCreatesPreviewScope(bool activeActor)
    {
        var actor = new SampleTrialFeedbackCorrectionTests.User("Developer");
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        var foreign = Employee(Guid.NewGuid(), Guid.NewGuid(), true, "Foreign");
        store.Seed(nameof(db.Employees), Employee(actor.CompanyId!.Value, actor.EmployeeId!.Value, activeActor, "Actor"), foreign);
        var scopes = new PreviewScopes();
        Assert.Null(await new GetSampleWorkPreviewQueryHandler(db, actor, null!, scopes)
            .Handle(new() { Persona = "SaleUser", EmployeeId = foreign.EmployeeId }, default));
        Assert.Equal(0, scopes.Created);
        if (!activeActor) Assert.Null(await new GetSampleWorkPreviewOptionsQueryHandler(db, actor).Handle(new(), default));
    }

    [Fact]
    public async Task InactiveTargetAndInvalidPersonaOrFilterAreRejectedBeforeScopes()
    {
        var actor = new SampleTrialFeedbackCorrectionTests.User("Developer");
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        var inactive = Employee(actor.CompanyId!.Value, Guid.NewGuid(), false, "Inactive");
        store.Seed(nameof(db.Employees), Employee(actor.CompanyId.Value, actor.EmployeeId!.Value, true, "Actor"), inactive);
        var scopes = new PreviewScopes();
        var handler = new GetSampleWorkPreviewQueryHandler(db, actor, null!, scopes);
        Assert.Null(await handler.Handle(new() { Persona = "LabUser", EmployeeId = inactive.EmployeeId }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(new() { Persona = "Developer", EmployeeId = actor.EmployeeId.Value }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(new() { Persona = "SaleUser", EmployeeId = actor.EmployeeId.Value, LabTeam = SampleLabTeamFilter.All }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(new() { Persona = "LabUser", EmployeeId = actor.EmployeeId.Value, Bucket = (SampleRequestWorkBucket)999 }, default));
        Assert.Equal(0, scopes.Created);
    }

    [Theory]
    [InlineData("LabUser", SampleRequestWorkAudience.Lab)]
    [InlineData("SaleUser", SampleRequestWorkAudience.Sale)]
    [InlineData("LabAdmin", SampleRequestWorkAudience.LabAdmin)]
    public async Task ValidPreviewUsesIsolatedIdentityAndCarriesActualActorReadBoundary(string persona, SampleRequestWorkAudience audience)
    {
        var actor = new SampleTrialFeedbackCorrectionTests.User("Developer");
        var targetId = Guid.NewGuid();
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        store.Seed(nameof(db.Employees), Employee(actor.CompanyId!.Value, actor.EmployeeId!.Value, true, "Actor"),
            Employee(actor.CompanyId.Value, targetId, true, "Target"));
        var visibility = VisibilityProxy.Create(actor);
        var scopes = new PreviewScopes();
        var result = await new GetSampleWorkPreviewQueryHandler(db, actor, visibility, scopes)
            .Handle(new() { Persona = persona, EmployeeId = targetId, Bucket = SampleRequestWorkBucket.MyActionRequired }, default);
        Assert.NotNull(result);
        Assert.True(result.ReadOnly);
        Assert.Equal(persona, result.Persona);
        Assert.Equal(targetId, result.EmployeeId);
        Assert.Equal(audience, scopes.CapturedQuery!.Audience);
        Assert.Equal(SampleRequestWorkBucket.MyActionRequired, scopes.CapturedQuery.Bucket);
        Assert.Equal(targetId, scopes.Session.User!.EmployeeId);
        Assert.Equal(actor.CompanyId, scopes.Session.User.CompanyId);
        Assert.Equal(new[] { persona }, scopes.Session.User.Roles);
        Assert.False(scopes.Session.User.IsInRole("Developer"));
        Assert.Same(((VisibilityProxy)visibility).Scope, scopes.Session.ActorReadScope);
        Assert.Equal(new[] { "Developer" }, actor.Roles); // authenticated actor remains unchanged
        Assert.True(scopes.Disposed);
        Assert.Equal(0, store.Saves);
        Assert.Empty(store.Added);
    }

    [Fact]
    public async Task ReadOnlyPipelineAllowsOnlyQueueReadsAndNeverInvokesCommandOrUnknownRead()
    {
        var session = new SampleWorkPreviewSession { User = new SampleWorkPreviewUser(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LabAdmin") };
        var data = new PagedResult<SampleRequestSampleTrialReportDto>([], 0, 1, 15);
        var reads = 0;
        var daily = new SampleWorkPreviewReadOnlyBehavior<GetSampleRequestDailyWorkQuery, PagedResult<SampleRequestSampleTrialReportDto>>(session);
        Assert.Same(data, await daily.Handle(new(), () => { reads++; return Task.FromResult(data); }, default));
        var trials = new SampleWorkPreviewReadOnlyBehavior<GetSampleRequestSampleTrialsQuery, PagedResult<SampleRequestSampleTrialReportDto>>(session);
        Assert.Same(data, await trials.Handle(new(), () => { reads++; return Task.FromResult(data); }, default));
        Assert.Equal(2, reads);
        var writes = 0;
        var command = new SampleWorkPreviewReadOnlyBehavior<UpdateSampleWorkScheduleCommand, OperationResult<Guid>>(session);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => command.Handle(new(), () =>
        {
            writes++;
            return Task.FromResult(OperationResult<Guid>.Ok(Guid.NewGuid()));
        }, default));
        var unknownRead = new SampleWorkPreviewReadOnlyBehavior<GetSampleWorkPreviewOptionsQuery, SampleWorkPreviewOptionsDto?>(session);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => unknownRead.Handle(new(), () =>
        {
            writes++;
            return Task.FromResult<SampleWorkPreviewOptionsDto?>(null);
        }, default));
        Assert.Equal(0, writes);
    }

    [Fact]
    public async Task PipelineWithoutPreviewDoesNotChangeNormalCommandExecution()
    {
        var session = new SampleWorkPreviewSession();
        var expected = OperationResult<Guid>.Ok(Guid.NewGuid());
        var pipeline = new SampleWorkPreviewReadOnlyBehavior<UpdateSampleWorkScheduleCommand, OperationResult<Guid>>(session);
        Assert.Same(expected, await pipeline.Handle(new(), () => Task.FromResult(expected), default));
    }

    private static Employee Employee(Guid company, Guid id, bool active, string name)
        => new() { CompanyId = company, EmployeeId = id, IsActive = active, FullName = name, ExternalId = "TEST" };

    public class VisibilityProxy : DispatchProxy
    {
        public ViewerScope Scope = null!;
        public static ICustomerVisibilityService Create(ICurrentUser actor)
        {
            var proxy = Create<ICustomerVisibilityService, VisibilityProxy>();
            ((VisibilityProxy)proxy).Scope = new(actor.CompanyId!.Value, actor.EmployeeId!.Value, false, false, new HashSet<Guid>(), new HashSet<Guid>(), DateTime.Now);
            return proxy;
        }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method?.Name == "BuildReadScopeAsync" ? Task.FromResult(Scope) : throw new NotSupportedException(method?.Name);
    }

    private sealed class PreviewScopes : IServiceScopeFactory, IServiceProvider, IServiceScope
    {
        public int Created;
        public bool Disposed;
        public SampleWorkPreviewSession Session { get; } = new();
        public GetSampleRequestDailyWorkQuery? CapturedQuery;
        public IServiceProvider ServiceProvider => this;
        public IServiceScope CreateScope() { Created++; return this; }
        public void Dispose() => Disposed = true;
        public object? GetService(Type type)
        {
            if (type == typeof(SampleWorkPreviewSession)) return Session;
            if (type == typeof(ISender))
            {
                var sender = DispatchProxy.Create<ISender, SenderProxy>();
                ((SenderProxy)sender).OnSend = query => CapturedQuery = query;
                return sender;
            }
            return null;
        }
    }

    public class SenderProxy : DispatchProxy
    {
        public Action<GetSampleRequestDailyWorkQuery> OnSend = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            var query = Assert.IsType<GetSampleRequestDailyWorkQuery>(args![0]);
            OnSend(query);
            return Task.FromResult(new PagedResult<SampleRequestSampleTrialReportDto>([], 0, 1, 15));
        }
    }
}
