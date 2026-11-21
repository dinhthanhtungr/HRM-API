using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.Work;
using HRM.Application.Features.PLM.SampleRequests.PersonalWork;
using HRM.Application.Features.Work.MyTasks;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.WorkTaskSchema;
using HRM.Domain.Enums.WorkTaskEnums;
using HRM.Domain.Enums.Notifications;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SamplePersonalWorkTests
{
    [Fact]
    public async Task ReminderDeploymentDefaultsOffWithoutTouchingPersistence()
    {
        var processor = new SamplePersonalReminderProcessor(null!, null!, null!, null!, new ConfigurationBuilder().Build());
        Assert.Equal(0, await processor.ProcessAsync(default));
    }

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    public void ReminderOnlyIncludesOptedInOpenUnsentTasks(bool notify, bool completed, bool alreadySent, bool expected)
    {
        var company = Guid.NewGuid(); var employee = Guid.NewGuid(); var request = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var (db, preferences) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        var (work, tasks) = ProductionOrderTestStore.CreateContext<IWorkTaskWriteDbContext>();
        preferences.Seed(nameof(db.SampleRequestUserPreferences), new SampleRequestUserPreference
            { CompanyId = company, EmployeeId = employee, SampleRequestId = request, NotifyOnDue = notify, ReminderTaskId = taskId });
        tasks.Seed(nameof(work.Employees), new HRM.Domain.Entities.HrSchema.Employee
            { CompanyId = company, EmployeeId = employee, IsActive = true });
        tasks.Seed(nameof(work.WorkTasks), new WorkTask
        {
            Id = taskId, CompanyId = company, CreatedBy = employee, AssignedToEmployeeId = employee,
            DueDate = DateTime.Today, DueReminderSentAt = alreadySent ? DateTime.Today : null,
            Status = completed ? WorkTaskStatus.Done : WorkTaskStatus.Pending,
            References = [new() { ReferenceType = WorkReferenceType.SampleRequest, ReferenceId = request }]
        });
        var processor = new SamplePersonalReminderProcessor(db, work, null!, null!, null!);
        Assert.Equal(expected, processor.DueTasks(DateTime.Now).Any());
    }
    [Fact]
    public async Task BatchOnlyReadsOwnNotesWithinCompanyAndReadableRequests()
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("SaleUser");
        var (db, data) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        var (work, tasks) = ProductionOrderTestStore.CreateContext<IWorkTaskReadDbContext>();
        data.Seed(nameof(db.Employees), new HRM.Domain.Entities.HrSchema.Employee
        { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId!.Value, IsActive = true });
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, IsActive = true };
        var hidden = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true };
        data.Seed(nameof(db.SampleRequests), request, hidden);
        data.Seed(nameof(db.SampleRequestUserPreferences), new SampleRequestUserPreference
        { CompanyId = user.CompanyId.Value, EmployeeId = user.EmployeeId!.Value, SampleRequestId = request.SampleRequestId, IsPinned = true });
        WorkTask Make(Guid company, Guid employee, string note) => new()
        {
            Id = Guid.NewGuid(), CompanyId = company, CreatedBy = employee, AssignedToEmployeeId = employee,
            Description = note, IsActive = true,
            References = [new() { ReferenceType = WorkReferenceType.SampleRequest, ReferenceId = request.SampleRequestId }]
        };
        tasks.Seed(nameof(work.WorkTasks), Make(user.CompanyId.Value, user.EmployeeId.Value, "mine"),
            Make(user.CompanyId.Value, Guid.NewGuid(), "another employee"), Make(Guid.NewGuid(), user.EmployeeId.Value, "another company"));
        var result = await new GetSamplePersonalWorkQueryHandler(db, work, user, SampleTrialFeedbackCorrectionTests.Visibility.Create(user))
            .Handle(new([request.SampleRequestId, hidden.SampleRequestId]), default);
        Assert.True(result.Success);
        var row = Assert.Single(result.Data!);
        Assert.Equal("mine", row.Note);
        Assert.True(row.IsPinned);
        Assert.False(row.NotifyOnDue);
        Assert.Empty(data.Added);
        Assert.Empty(tasks.Added);
    }

    [Fact]
    public void MyTasksKeepsSampleNotesPersonalAndSeparatesCrmTasks()
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("SaleUser");
        var (read, data) = ProductionOrderTestStore.CreateContext<IWorkTaskReadDbContext>();
        var (write, writeData) = ProductionOrderTestStore.CreateContext<IWorkTaskWriteDbContext>();
        var actor = new MyTaskActor(user.CompanyId!.Value, user.EmployeeId!.Value);
        WorkTask Make(Guid company, Guid employee, WorkReferenceType? type) => new()
        {
            Id = Guid.NewGuid(), CompanyId = company, CreatedBy = employee, AssignedToEmployeeId = employee,
            References = type.HasValue ? [new() { ReferenceType = type.Value }] : []
        };
        var personal = Make(actor.CompanyId, actor.EmployeeId, null);
        var sample = Make(actor.CompanyId, actor.EmployeeId, WorkReferenceType.SampleRequest);
        sample.References.Single().ReferenceId = Guid.NewGuid();
        var crm = Make(actor.CompanyId, actor.EmployeeId, WorkReferenceType.Customer);
        data.Seed(nameof(read.WorkTasks), personal, sample, crm,
            Make(actor.CompanyId, Guid.NewGuid(), WorkReferenceType.SampleRequest),
            Make(Guid.NewGuid(), actor.EmployeeId, WorkReferenceType.SampleRequest));
        writeData.Seed(nameof(write.WorkTasks), personal, sample, crm,
            Make(actor.CompanyId, Guid.NewGuid(), WorkReferenceType.SampleRequest));
        var support = new MyTaskSupport(read, write, user, null!);
        Assert.Equal(2, support.BuildPersonalTaskQuery(actor).Count());
        Assert.Equal(2, support.BuildWritablePersonalTaskQuery(actor).Count());
        Assert.Equal(crm.Id, support.BuildCustomerFollowUpTaskQuery(actor).Single().Id);
        Assert.All(support.ProjectTasks(support.BuildPersonalTaskQuery(actor), DateTime.Today),
            row => Assert.Equal(WorkTaskSourceType.Personal, row.SourceType));
        var projected = support.ProjectTasks(support.BuildPersonalTaskQuery(actor), DateTime.Today).ToList();
        Assert.Equal(sample.References.Single().ReferenceId, projected.Single(x => x.WorkTaskId == sample.Id).SampleRequestId);
        Assert.Null(projected.Single(x => x.WorkTaskId == personal.Id).SampleRequestId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InvalidOrAnonymousMutationDoesNotReachPersistence(bool authenticated)
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("LabUser", authenticated);
        var result = await new SaveSamplePersonalWorkCommandHandler(null!, null!, user, null!).Handle(
            new(Guid.NewGuid(), new() { SaveNote = true, TaskId = Guid.NewGuid(), Note = null }), default);
        Assert.False(result.Success);
    }

    [Fact]
    public void PrivateReminderCannotIncludeObserversRolesTeamsOrAnotherRecipient()
    {
        var owner = Guid.NewGuid();
        var request = new PublishNotificationRequest { Topic = TopicNotifications.WorkTaskDue,
            PersonalReminderOwnerId = owner, TargetUserIds = [owner] };
        Assert.True(PersonalReminderAudience.IsOwnerOnly(request, owner));
        request.TargetUserIds = [owner, Guid.NewGuid()];
        Assert.Throws<InvalidOperationException>(() => PersonalReminderAudience.IsOwnerOnly(request, owner));
        request.TargetUserIds = [owner];
        request.TargetRoles = ["Developer"];
        Assert.Throws<InvalidOperationException>(() => PersonalReminderAudience.IsOwnerOnly(request, owner));
        Assert.False(PersonalReminderAudience.IsOwnerOnly(new() { Topic = TopicNotifications.CustomerFollowUpTaskDue }, owner));
    }

    [Fact]
    public void PersonalWorkModelAndMyTaskQueriesTranslateWithoutDatabaseConnection()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var model = db.Model.FindEntityType(typeof(SampleRequestUserPreference))!;
        Assert.False((bool)model.FindProperty(nameof(SampleRequestUserPreference.NotifyOnDue))!.GetDefaultValue()!);
        var user = new SampleTrialFeedbackCorrectionTests.User("LabUser");
        var support = new MyTaskSupport(db, db, user, null!);
        var actor = new MyTaskActor(user.CompanyId!.Value, user.EmployeeId!.Value);
        Assert.Contains("WorkTaskReferences", support.BuildPersonalTaskQuery(actor).ToQueryString());
        Assert.Contains("WorkTaskReferences", support.BuildCustomerFollowUpTaskQuery(actor).ToQueryString());
        var processor = new SamplePersonalReminderProcessor(db, db, null!, null!, null!);
        Assert.Contains("NotifyOnDue", processor.DueTasks(DateTime.Now).ToQueryString());
    }
}
