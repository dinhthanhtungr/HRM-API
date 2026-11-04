using System.Text.Json;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    // 5 roles × 3 private areas × 32 distinct security states = 480 cases.
    public static IEnumerable<object[]> ChatAccessCases()
    {
        foreach (var role in new[] { "President", "Developer", "LabUser", "PLPUUser", "SaleUser" })
        foreach (var area in new[] { InternalMailRelatedType.ConversationTechnical,
                     InternalMailRelatedType.ConversationPricing, InternalMailRelatedType.ConversationCustom })
        foreach (var member in new[] { false, true })
        foreach (var sameCompany in new[] { false, true })
        foreach (var activeRoot in new[] { false, true })
        foreach (var activeGroup in new[] { false, true })
        foreach (var authenticated in new[] { false, true })
            yield return [role, area, member, sameCompany, activeRoot, activeGroup, authenticated];
    }

    [Theory]
    [MemberData(nameof(ChatAccessCases))]
    public void ChatMatrix_ConversationAndMessagesNeverBypassScope(string role, InternalMailRelatedType area,
        bool member, bool sameCompany, bool activeRoot, bool activeGroup, bool authenticated)
    {
        var user = new User(role) { IsAuthenticated = authenticated };
        var store = CreateStore(user, area, member);
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        store.Root.IsActive = activeRoot;
        store.Child.IsActive = activeGroup;
        if (!sameCompany) user.CompanyId = Guid.NewGuid();
        var expected = authenticated && sameCompany && activeRoot && activeGroup &&
            (member || role == "Developer");
        var service = new InternalMailAreaAccessService(store.Db, user);
        Assert.Equal(expected, service.Conversations().Any(c => c.InternalConversationId == store.Child.InternalConversationId));
        Assert.Equal(expected, service.Messages().Any(m => m.InternalConversationId == store.Child.InternalConversationId));
        Assert.Equal(expected, service.Messages(InternalMailAreaAccessService.AreaOf(area)).Any());
        Assert.Empty(service.Messages("unknown-area"));
        // Reading as an observer must not silently create a membership.
        Assert.Equal(member ? 1 : 0, store.Child.Participants.Count);
    }

    public static IEnumerable<object[]> ChatDeliveryCases()
    {
        foreach (var mention in new[] { false, true })
        foreach (var muted in new[] { false, true })
        foreach (var member in new[] { false, true })
        foreach (var archived in new[] { false, true })
        foreach (var activeEmployee in new[] { false, true })
        foreach (var sameCompany in new[] { false, true })
            yield return [mention, muted, member, archived, activeEmployee, sameCompany];
    }

    [Theory]
    [MemberData(nameof(ChatDeliveryCases))]
    public void ChatMatrix_MentionBypassesOnlyMute(bool mention, bool muted, bool member,
        bool archived, bool activeEmployee, bool sameCompany)
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, true);
        var participant = store.Child.Participants.Single();
        participant.IsActive = member;
        participant.IsMuted = muted;
        var employeeId = user.EmployeeId!.Value;
        var company = user.CompanyId!.Value;
        var notification = new Notification { Id = Guid.NewGuid(), CompanyId = company,
            PayloadJson = JsonSerializer.Serialize(new { deliveryMentionEmployeeIds = mention ? new[] { employeeId } : [] }) };
        var memory = (MemoryStore)(object)store.Db;
        memory.Values["Employees"] = new MemorySet<Employee>([new() { EmployeeId = employeeId,
            CompanyId = sameCompany ? company : Guid.NewGuid(), IsActive = activeEmployee }]);
        memory.Values["NotificationUserStates"] = new MemorySet<NotificationUserState>([new() {
            NotificationId = notification.Id, UserId = employeeId, IsArchived = archived }]);
        memory.Values["QueryInternalMailNotificationLinks"] = new[] { new InternalMailNotificationLink {
            NotificationId = notification.Id, HasConversation = true, ConversationId = store.Child.InternalConversationId } }.AsQueryable();
        var recipients = new InternalMailAreaAccessService(store.Db, user).DeliveryRecipients(notification).ToArray();
        var expected = member && !archived && activeEmployee && sameCompany && (!muted || mention);
        Assert.Equal(expected ? new[] { employeeId } : [], recipients);
    }

    public static IEnumerable<object[]> ChatReplyInboxCases()
    {
        foreach (var member in new[] { false, true })
        foreach (var deleted in new[] { false, true })
        foreach (var sentByMe in new[] { false, true })
        foreach (var repliesToMe in new[] { false, true })
        foreach (var sameConversation in new[] { false, true })
            yield return [member, deleted, sentByMe, repliesToMe, sameConversation];
    }

    [Theory]
    [MemberData(nameof(ChatReplyInboxCases))]
    public void ChatMatrix_ReplyCountsAsMineOnlyInsideVisibleConversation(bool member, bool deleted,
        bool sentByMe, bool repliesToMe, bool sameConversation)
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, member);
        var message = store.Db.InternalMessages.Single();
        message.IsDeleted = deleted;
        message.SenderEmployeeId = sentByMe ? user.EmployeeId!.Value : Guid.NewGuid();
        message.PayloadJson = null;
        message.ReplyToMessage = new InternalMessage {
            InternalMessageId = Guid.NewGuid(),
            InternalConversationId = sameConversation ? store.Child.InternalConversationId : Guid.NewGuid(),
            SenderEmployeeId = repliesToMe ? user.EmployeeId!.Value : Guid.NewGuid() };
        message.ReplyToMessageId = message.ReplyToMessage.InternalMessageId;
        var result = new InternalMailAreaAccessService(store.Db, user).MentionedMessages().ToArray();
        Assert.Equal(member && !deleted && !sentByMe && repliesToMe && sameConversation, result.Length == 1);
    }

    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    [InlineData(50, false)] [InlineData(50, true)]
    [InlineData(51, false)] [InlineData(51, true)]
    [InlineData(100, false)] [InlineData(100, true)]
    public async Task ChatMatrix_MentionLimitCountsDistinctValidMembers(int count, bool duplicate)
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, true);
        var employees = Enumerable.Range(0, count).Select(index => new Employee {
            EmployeeId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, IsActive = true,
            FullName = $"Nhân viên kiểm thử {index}" }).ToArray();
        var memory = (MemoryStore)(object)store.Db;
        memory.Values["Employees"] = new MemorySet<Employee>(employees);
        memory.Values["InternalConversationParticipants"] = new MemorySet<InternalConversationParticipant>(
            employees.Select(employee => new InternalConversationParticipant {
                EmployeeId = employee.EmployeeId, InternalConversationId = store.Child.InternalConversationId,
                Conversation = store.Child, IsActive = true }));
        var ids = employees.Select(employee => employee.EmployeeId).ToArray();
        var result = await new InternalMailMentionResolver(store.Db, new(store.Db, user)).ResolveAsync(
            user.CompanyId.Value, store.Child.InternalConversationId, duplicate ? ids.Concat(ids).ToArray() : ids, default);
        Assert.Equal(count <= 50, result.Success);
        if (count <= 50)
        {
            Assert.Equal(count, result.Data!.Count);
            Assert.Equal(ids.Order(), result.Data.Select(mention => mention.EmployeeId).Order());
        }
    }
}
