using HRM.Application.Commons.Authorization;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Identity;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData(InternalMailRelatedType.Internal)]
    [InlineData(InternalMailRelatedType.SampleRequest)]
    [InlineData(InternalMailRelatedType.Quotation)]
    [InlineData(InternalMailRelatedType.ConversationTechnical)]
    [InlineData(InternalMailRelatedType.ConversationPricing)]
    [InlineData(InternalMailRelatedType.ConversationCustom)]
    public void DeveloperCanReadAndListCompanyConversationsWithoutCreatingMembership(InternalMailRelatedType type)
    {
        var user = new User("Developer") { HasExplicitPermissionSet = true, Permissions = [] };
        var store = CreateStore(user, type, false);
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.Contains(access.Conversations(), c => c.InternalConversationId == store.Child.InternalConversationId);
        Assert.Single(access.Messages());
        var row = Assert.Single(access.InboxConversations(false).Where(c => c.InternalConversationId == store.Child.InternalConversationId));
        Assert.False(row.IsMember);
        Assert.Empty(store.Child.Participants);
    }

    [Theory]
    [InlineData("SaleUser")]
    [InlineData("LabUser")]
    [InlineData("Admin")]
    [InlineData("President")]
    public void CompanyObserverClaimCannotGrantNonDevelopersUnrelatedPrivateChats(string role)
    {
        var user = new User(role) { HasExplicitPermissionSet = true, Permissions = [ApplicationPermissions.InternalMail.ObserveCompanyConversations] };
        var store = CreateStore(user, InternalMailRelatedType.ConversationCustom, false);
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.False(access.CanObserveCompanyConversations);
        Assert.DoesNotContain(access.Conversations(), c => c.InternalConversationId == store.Child.InternalConversationId);
        Assert.Empty(access.Messages());
        Assert.Empty(access.InboxConversations());
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("inactive")]
    [InlineData("inactive-parent")]
    [InlineData("anonymous")]
    [InlineData("no-company")]
    public void DeveloperObservationStillFailsClosedAtCompanyAndRecordBoundaries(string boundary)
    {
        var user = new User("Developer");
        var store = CreateStore(user, InternalMailRelatedType.ConversationCustom, false);
        if (boundary == "foreign") store.Child.CompanyId = Guid.NewGuid();
        if (boundary == "inactive") store.Child.IsActive = false;
        if (boundary == "inactive-parent") store.Root.IsActive = false;
        if (boundary == "anonymous") user.IsAuthenticated = false;
        if (boundary == "no-company") user.CompanyId = null;
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.DoesNotContain(access.Conversations(), c => c.InternalConversationId == store.Child.InternalConversationId);
        Assert.DoesNotContain(access.InboxConversations(), c => c.InternalConversationId == store.Child.InternalConversationId);
        Assert.Empty(access.Messages());
    }

    private static InternalConversationParticipantDto[] ObserverParticipantFixture(MemoryStore memory, Guid company, bool activeRole = true)
    {
        var hidden = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = company, FullName = "Developer with any name", IsActive = true };
        var ordinary = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = company, FullName = "Ordinary member", IsActive = true };
        var account = new ApplicationUser { Id = Guid.NewGuid(), EmployeeId = hidden.EmployeeId };
        var role = new ApplicationRole { Id = Guid.NewGuid(), Name = "Developer", NormalizedName = "DEVELOPER" };
        memory.Values["Employees"] = new MemorySet<Employee>([hidden, ordinary]);
        memory.Values["Users"] = new MemorySet<ApplicationUser>([account]);
        memory.Values["Roles"] = new MemorySet<ApplicationRole>([role]);
        memory.Values["UserRoles"] = new MemorySet<ApplicationUserRole>([new() { UserId = account.Id, RoleId = role.Id, IsActive = activeRole }]);
        return [new() { EmployeeId = hidden.EmployeeId, EmployeeName = hidden.FullName }, new() { EmployeeId = ordinary.EmployeeId, EmployeeName = ordinary.FullName }];
    }

    [Theory]
    [InlineData(InternalMailRelatedType.Internal)]
    [InlineData(InternalMailRelatedType.SampleRequest)]
    [InlineData(InternalMailRelatedType.Quotation)]
    [InlineData(InternalMailRelatedType.ConversationTechnical)]
    [InlineData(InternalMailRelatedType.ConversationPricing)]
    [InlineData(InternalMailRelatedType.ConversationCustom)]
    public async Task NonDeveloperParticipantsAndReaderGroupsNeverContainRoleDevelopers(InternalMailRelatedType type)
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, type, true);
        var source = ObserverParticipantFixture((MemoryStore)(object)store.Db, user.CompanyId!.Value);
        var access = new InternalMailAreaAccessService(store.Db, user);
        var visible = await access.VisibleParticipantsAsync(type, source, default);
        var result = await access.WithReaderGroupsAsync(visible, default);
        Assert.Equal(source[1].EmployeeId, Assert.Single(result).EmployeeId);
        Assert.DoesNotContain(result, p => p.ReaderGroup?.LeaderEmployeeId == source[0].EmployeeId);
    }

    [Theory]
    [InlineData("Developer", true, 2)]
    [InlineData("President", true, 1)]
    [InlineData("Admin", true, 1)]
    [InlineData("SaleUser", true, 1)]
    [InlineData("SaleUser", false, 2)]
    public async Task DeveloperVisibilityUsesActiveRoleRatherThanHardCodedNames(string viewer, bool activeRole, int expectedCount)
    {
        var user = new User(viewer);
        var store = CreateStore(user, InternalMailRelatedType.Internal, true);
        var source = ObserverParticipantFixture((MemoryStore)(object)store.Db, user.CompanyId!.Value, activeRole);
        var visible = await new InternalMailAreaAccessService(store.Db, user).VisibleParticipantsAsync(InternalMailRelatedType.Internal, source, default);
        Assert.Equal(expectedCount, visible.Count);
    }

    [Fact]
    public async Task InactiveEmployeeWithActiveDeveloperRoleStillStaysHiddenFromParticipantMetadata()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.Internal, true);
        var source = ObserverParticipantFixture((MemoryStore)(object)store.Db, user.CompanyId!.Value);
        store.Db.Employees.Single(e => e.EmployeeId == source[0].EmployeeId).IsActive = false;
        var visible = await new InternalMailAreaAccessService(store.Db, user).VisibleParticipantsAsync(InternalMailRelatedType.Internal, source, default);
        Assert.Equal(source[1].EmployeeId, Assert.Single(visible).EmployeeId);
    }

    [Fact]
    public async Task HiddenDeveloperMessageAuthorRemainsVisibleToOrdinaryConversationMember()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.Internal, true);
        var source = ObserverParticipantFixture((MemoryStore)(object)store.Db, user.CompanyId!.Value);
        var message = store.Db.InternalMessages.Single();
        message.SenderEmployeeId = source[0].EmployeeId;
        message.SenderEmployee = new Employee { EmployeeId = source[0].EmployeeId, FullName = source[0].EmployeeName };
        message.Body = "Public message authored by Developer";
        var access = new InternalMailAreaAccessService(store.Db, user);
        var result = await new HRM.Application.Features.InternalMail.Queries.GetMessages.GetInternalMessagesQueryHandler(
            store.Db, user, access, new CurrentUserPermissionService(user)).Handle(new() { ConversationId = store.Child.InternalConversationId }, default);
        Assert.Equal(source[0].EmployeeName, Assert.Single(result!.Items).SenderName);
        Assert.Single(await access.VisibleParticipantsAsync(InternalMailRelatedType.Internal, source, default));
    }

    [Theory]
    [InlineData("Developer")]
    [InlineData("SaleUser")]
    public void ObserverInboxProjectionTranslatesToPostgresWithoutJoiningUnrelatedMembers(string role)
    {
        var user = new User(role);
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var access = new InternalMailAreaAccessService(db, user);
        var sql = access.InboxConversations(false).OrderByDescending(row => row.Conversation.Messages.Max(m => (DateTime?)m.SentAt))
            .Select(row => new { row.InternalConversationId, row.IsArchived, row.IsMuted,
                LastSender = row.Conversation.Messages.Where(m => !m.IsDeleted).Select(m => m.SenderEmployee.FullName).FirstOrDefault() }).ToQueryString();
        Assert.NotEmpty(sql);
    }
}
