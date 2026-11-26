using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.InternalMail.Queries.GetConversationDetail;
using HRM.Application.Features.InternalMail.Queries.GetParticipants;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MessagesExposeCommercialReasonWithoutCheckingCurrentStandard(bool changed)
    {
        var user = new User("President");
        var store = CreateStore(user, InternalMailRelatedType.Quotation, true);
        var quote = new HRM.Domain.Entities.CustomerSchema.Quotation
        {
            QuotationId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            UpdatedDate = new DateTime(2026, 10, 22),
            Status = HRM.Domain.Enums.CustomerEnum.QuotationStatus.PendingSendConfirmation
        };
        store.Child.RelatedId = quote.QuotationId;
        ((MemoryStore)(object)store.Db).Values["Quotations"] =
            new MemorySet<HRM.Domain.Entities.CustomerSchema.Quotation>([quote]);
        var message = store.Db.InternalMessages.Single();
        message.MessageType = InternalMessageType.Action;
        message.SenderEmployee = new() { FullName = "Sale" };
        var payload = new HRM.Application.Features.CRM.Quotations.Dtos.QuotationSendConfirmationPayload
        {
            QuotationId = quote.QuotationId, ConversationId = store.Child.InternalConversationId,
            RequestMessageId = message.InternalMessageId, ExpectedUpdatedDate = quote.UpdatedDate,
            CommercialFingerprintVersion = 1,
            CommercialFingerprint = HRM.Application.Features.CRM.Quotations.Services.QuotationConfirmationCommercialRules.Fingerprint(quote),
            Comparison = [new(Guid.NewGuid(), "P1", Guid.NewGuid(), quote.UpdatedDate.Value, 0, null, true, false, 100, 90)]
        };
        message.PayloadJson = System.Text.Json.JsonSerializer.Serialize(payload,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        if (changed) quote.PaymentTerms = "New payment condition";
        var handler = new HRM.Application.Features.InternalMail.Queries.GetMessages.GetInternalMessagesQueryHandler(
            store.Db, user, new InternalMailAreaAccessService(store.Db, user),
            new HRM.Application.Commons.Authorization.CurrentUserPermissionService(user));
        var response = await handler.Handle(new() { ConversationId = store.Child.InternalConversationId }, default);
        var item = Assert.Single(response!.Items);
        Assert.Equal(!changed, item.CanConfirmSent);
        Assert.Equal(changed ? "quotation.confirm.commercial_changed" : null, item.ConfirmSentBlockedReasonCode);
    }

    [Theory]
    [InlineData("President", true, true, false, 3)]
    [InlineData("SaleUser", true, false, false, 3)]
    [InlineData("Developer", true, true, false, 3)]
    [InlineData("President", false, false, false, 3)]
    [InlineData("President", true, false, true, 3)]
    [InlineData("President", true, true, false, 0)]
    [InlineData("President", true, true, false, 1)]
    [InlineData("President", true, true, false, 2)]
    public async Task QuotationSendButtonDependsOnViewerAndPendingState(string role, bool pending, bool expected, bool staleSnapshot, int ruleVersion)
    {
        var user = new User(role);
        var store = CreateStore(user, InternalMailRelatedType.Quotation, true);
        var quote = new HRM.Domain.Entities.CustomerSchema.Quotation
        {
            QuotationId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, UpdatedDate = new DateTime(2026, 10, 17),
            Status = pending ? HRM.Domain.Enums.CustomerEnum.QuotationStatus.PendingSendConfirmation
                : HRM.Domain.Enums.CustomerEnum.QuotationStatus.Sent
        };
        store.Child.RelatedId = quote.QuotationId;
        ((MemoryStore)(object)store.Db).Values["Quotations"] =
            new MemorySet<HRM.Domain.Entities.CustomerSchema.Quotation>([quote]);
        var message = store.Db.InternalMessages.Single();
        message.MessageType = InternalMessageType.Action;
        message.SenderEmployee = new() { FullName = "Sale" };
        message.PayloadJson = System.Text.Json.JsonSerializer.Serialize(
            new HRM.Application.Features.CRM.Quotations.Dtos.QuotationSendConfirmationPayload
            {
                ExpectedUpdatedDate = staleSnapshot ? quote.UpdatedDate.Value.AddSeconds(-1) : quote.UpdatedDate,
                ComparisonRuleVersion = ruleVersion,
                Comparison = [new(Guid.NewGuid(), "P1", Guid.NewGuid(), quote.UpdatedDate.Value, 0, null, true, false, 100, 90)],
                QuotationId = quote.QuotationId, RequestMessageId = message.InternalMessageId,
                ConversationId = store.Child.InternalConversationId
            }, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        var handler = new HRM.Application.Features.InternalMail.Queries.GetMessages.GetInternalMessagesQueryHandler(
            store.Db, user, new InternalMailAreaAccessService(store.Db, user),
            new HRM.Application.Commons.Authorization.CurrentUserPermissionService(user));
        var result = await handler.Handle(new() { ConversationId = store.Child.InternalConversationId }, default);
        Assert.Equal(expected, Assert.Single(result!.Items).CanConfirmSent);
    }

    [Theory]
    [InlineData("technical", 1)]
    [InlineData("pricing", 0)]
    [InlineData("unknown", 0)]
    [InlineData(null, 1)]
    public async Task Messages_PreserveAreaCountsPaginationAndRevokedMembership(string? area, int count)
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, true);
        var message = store.Db.InternalMessages.Single();
        message.SenderEmployee = new() { FullName = "Sender" };
        var areas = new InternalMailAreaAccessService(store.Db, user);
        var handler = new HRM.Application.Features.InternalMail.Queries.GetMessages.GetInternalMessagesQueryHandler(
            store.Db, user, areas, new HRM.Application.Commons.Authorization.CurrentUserPermissionService(user));
        var request = new HRM.Application.Features.InternalMail.Queries.GetMessages.GetInternalMessagesQuery
        {
            ConversationId = store.Child.InternalConversationId, AreaCode = area, PageNumber = 1, PageSize = 30
        };
        var result = await handler.Handle(request, default);
        Assert.NotNull(result);
        Assert.Equal(count, result.TotalCount);
        Assert.Equal(count, result.Items.Count);
        if (count > 0) Assert.Equal(message.InternalMessageId, result.Items.Single().MessageId);
        request = new HRM.Application.Features.InternalMail.Queries.GetMessages.GetInternalMessagesQuery
        {
            ConversationId = store.Child.InternalConversationId, AreaCode = area, PageNumber = 2, PageSize = 30
        };
        result = await handler.Handle(request, default);
        Assert.Equal(count, result!.TotalCount);
        Assert.Empty(result.Items);
        store.Child.Participants.Single().IsActive = false;
        Assert.Null(await handler.Handle(request, default));
        store.Child.Participants.Single().IsActive = true;
        user.CompanyId = Guid.NewGuid();
        Assert.Null(await handler.Handle(request, default));
    }

    [Theory]
    [InlineData("PIG", true)]
    [InlineData(" cmb ", true)]
    [InlineData("OTHER", false)]
    public async Task EligibleParticipants_MatchExistingRecipientQuery(string category, bool color)
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, true);
        var active = new HRM.Domain.Entities.HrSchema.Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId!.Value, IsActive = true };
        var inactive = new HRM.Domain.Entities.HrSchema.Employee { EmployeeId = Guid.NewGuid(), CompanyId = user.CompanyId.Value, IsActive = false };
        var foreign = new HRM.Domain.Entities.HrSchema.Employee { EmployeeId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true };
        ((MemoryStore)(object)store.Db).Values[nameof(store.Db.Employees)] = new MemorySet<HRM.Domain.Entities.HrSchema.Employee>([active, inactive, foreign]);
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        store.Root.RelatedId = Guid.NewGuid();
        var proxy = (MemoryStore)(object)store.Db;
        proxy.Values[nameof(store.Db.SampleRequests)] = new MemorySet<HRM.Domain.Entities.SampleRequestSchema.SampleRequest>([
            new() { SampleRequestId = store.Root.RelatedId.Value, CompanyId = user.CompanyId.Value,
                Product = new() { CompanyId = user.CompanyId.Value,
                    Category = new() { CompanyId = user.CompanyId.Value, ExternalId = category } } }
        ]);
        proxy.Values[nameof(store.Db.MemberInGroups)] = new MemorySet<HRM.Domain.Entities.CompanySchema.MemberInGroup>([
            new() { IsActive = true, IsAdmin = true, Profile = active.EmployeeId, ProfileNavigation = active,
                Group = new() { CompanyId = user.CompanyId.Value, GroupType = "QAQC.RD" } }
        ]);
        var areas = new InternalMailAreaAccessService(store.Db, user);
        Guid[] members = [active.EmployeeId, inactive.EmployeeId, foreign.EmployeeId];
        var before = areas.Recipients(user.CompanyId.Value, "technical", store.Child.InternalConversationId)
            .Where(id => members.Contains(id)).ToHashSet();
        var after = await areas.EligibleParticipantsAsync(user.CompanyId.Value, store.Child.InternalConversationId, members, default);
        Assert.True(before.SetEquals(after));
        Assert.Equal(!color, after.Contains(active.EmployeeId));
        Assert.Equal(color ? 0 : 1, after.Count);
        Assert.Empty(await areas.EligibleParticipantsAsync(user.CompanyId.Value, store.Child.InternalConversationId, [], default));
    }

    [Theory]
    [InlineData("technical", true)]
    [InlineData(null, true)]
    [InlineData("pricing", false)]
    [InlineData("unknown", false)]
    public async Task Participants_PreserveMembershipAndAreaChecks(string? area, bool allowed)
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, true);
        var participant = store.Child.Participants.Single();
        participant.Employee = new() { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId!.Value, FullName = "Member", IsActive = true };
        ((MemoryStore)(object)store.Db).Values[nameof(store.Db.Employees)] = new MemorySet<HRM.Domain.Entities.HrSchema.Employee>([participant.Employee]);
        var handler = new GetInternalConversationParticipantsQueryHandler(new(store.Db, user), user);
        var request = new GetInternalConversationParticipantsQuery { ConversationId = store.Child.InternalConversationId, AreaCode = area };
        var result = await handler.Handle(request, default);
        if (allowed) Assert.Equal("Member", Assert.Single(result!).EmployeeName);
        else Assert.Null(result);
        participant.IsActive = false;
        Assert.Null(await handler.Handle(request, default));
        participant.IsActive = true;
        user.CompanyId = Guid.NewGuid();
        Assert.Null(await handler.Handle(request, default));
    }

    [Fact]
    public async Task AreaDescriptions_KeepUnreadCountsAndDoNotExposeUnjoinedChildren()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, false);
        store.Root.Participants.Add(new() { EmployeeId = user.EmployeeId!.Value, IsActive = true, Role = InternalConversationParticipantRole.Owner });
        var proxy = (MemoryStore)(object)store.Db;
        proxy.Values[nameof(store.Db.QueryInternalConversationNotificationUnreadCounts)] = new AsyncQuery<HRM.Application.Abstractions.Persistence.InternalMail.InternalConversationNotificationUnreadCount>([
            new() { ConversationId = store.Root.InternalConversationId, UnreadCount = 3 },
            new() { ConversationId = store.Child.InternalConversationId, UnreadCount = 9 }
        ]);
        var dto = new InternalConversationDetailDto
        {
            ConversationId = store.Root.InternalConversationId, RelatedType = store.Root.RelatedType, UnreadCount = 2,
            Participants = [new() { EmployeeId = user.EmployeeId.Value, Role = InternalConversationParticipantRole.Owner }]
        };
        var areas = await new InternalMailAreaAccessService(store.Db, user).DescribeAsync(dto, default);
        Assert.Equal(3, Assert.Single(areas, a => a.Code == "general").UnreadCount);
        Assert.DoesNotContain(areas, a => a.Code == "technical");
        Assert.True(Assert.Single(areas, a => a.Code == "pricing").CanCreate);
        dto.UnreadCount = 5;
        Assert.Equal(3, Assert.Single(await new InternalMailAreaAccessService(store.Db, user).DescribeAsync(dto, default), a => a.Code == "general").UnreadCount);
    }

    [Theory]
    [InlineData("SaleUser", InternalMailRelatedType.ConversationTechnical, true)]
    [InlineData("LabUser", InternalMailRelatedType.ConversationPricing, true)]
    [InlineData("President", InternalMailRelatedType.ConversationTechnical, false)]
    [InlineData("Developer", InternalMailRelatedType.ConversationPricing, false)]
    [InlineData("SaleUser", InternalMailRelatedType.ConversationTechnical, false)]
    public void PrivateArea_ReadAccessRequiresMembershipOrCompanyDeveloper(string role, InternalMailRelatedType type, bool member)
    {
        var user = new User(role);
        var store = CreateStore(user, type, member);
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.Equal(member || role == "Developer", access.Conversations().Any(c => c.InternalConversationId == store.Child.InternalConversationId));
        Assert.Equal(member || role == "Developer", access.Messages().Any());
        Assert.Empty(access.Messages("unknown"));
        Assert.Empty(access.Messages(type == InternalMailRelatedType.ConversationTechnical ? "pricing" : "technical"));
    }

    [Fact]
    public void ParentOwnerDoesNotInheritPrivateHistory_AndRevocationIsImmediate()
    {
        var user = new User("President");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, false);
        store.Root.Participants.Add(new() { EmployeeId = user.EmployeeId!.Value, IsActive = true, Role = InternalConversationParticipantRole.Owner });
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.Empty(access.Messages());
        var participant = new InternalConversationParticipant { EmployeeId = user.EmployeeId.Value, IsActive = true };
        store.Child.Participants.Add(participant);
        Assert.Single(access.Messages());
        participant.IsActive = false;
        Assert.Empty(access.Messages());
    }

    [Fact]
    public void MembershipDoesNotBypassTenant_OrInactiveParent()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationPricing, true);
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.Single(access.Messages());
        store.Root.IsActive = false;
        Assert.Empty(access.Messages());
        store.Root.IsActive = true;
        user.CompanyId = Guid.NewGuid();
        Assert.Empty(access.Messages());
    }

    [Fact]
    public void AnonymousUserCannotReadEvenWithMembership()
    {
        var user = new User("SaleUser") { IsAuthenticated = false };
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, true);
        Assert.Empty(new InternalMailAreaAccessService(store.Db, user).Messages());
    }

    [Fact]
    public void HttpPayloadCannotSetTrustedForwardFlag()
    {
        var command = System.Text.Json.JsonSerializer.Deserialize<HRM.Application.Features.InternalMail.Commands.SendMessage.SendInternalMessageCommand>(
            "{\"Body\":\"text\",\"IsForwarded\":true}")!;
        var flag = command.GetType().GetProperty("IsForwarded", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Equal(false, flag.GetValue(command));
    }

    [Fact]
    public void GenericAttachmentRoutesCannotBypassPrivateMembership()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, true);
        var message = store.Db.InternalMessages.First();
        var attachment = new HRM.Domain.Entities.AttachmentSchema.AttachmentModel
        {
            Slot = HRM.Domain.Enums.Attachment.AttachmentSlot.InternalMail,
            InternalMessageAttachments = [new() { InternalMessageId = message.InternalMessageId }]
        };
        var access = new InternalMailAreaAccessService(store.Db, user);
        var source = new[] { attachment }.AsQueryable();
        Assert.Single(access.Attachments(source));
        message.IsDeleted = true;
        Assert.Empty(access.Attachments(source));
        message.IsDeleted = false;
        store.Child.Participants.Single().IsActive = false;
        Assert.Empty(access.Attachments(source));
        attachment.InternalMessageAttachments.Clear();
        Assert.Empty(access.Attachments(source));
    }

    [Fact]
    public void QueriesTranslateOnPostgres_WithoutNewEntityColumnsOrRoleClaims()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var user = new User("SaleUser");
        var access = new InternalMailAreaAccessService(db, user);
        var sql = access.Messages("technical").Select(m => m.InternalMessageId).ToQueryString();
        Assert.Contains("InternalConversationParticipants", sql);
        Assert.Contains("CompanyId", sql);
        Assert.Contains("QAQC.RD", sql);
        Assert.DoesNotContain("AspNetRoleClaims", sql);
        Assert.Null(db.Model.FindEntityType(typeof(InternalMessage))!.FindProperty("AreaCode"));
        Assert.Null(db.Model.FindEntityType(typeof(Notification))!.FindProperty("ConversationId"));
        Assert.NotEmpty(access.States().Select(s => s.NotificationId).ToQueryString());
        Assert.NotEmpty(access.NotificationUnreadCounts().ToQueryString());
        Assert.NotEmpty(access.Attachments(db.AttachmentModels).ToQueryString());
        Assert.NotEmpty(access.Conversations().Select(InternalConversationDetailProjection.ForEmployee(user.EmployeeId!.Value, access.Messages())).ToQueryString());
        Assert.NotEmpty(access.DeliveryRecipients(new Notification { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value }).ToQueryString());
    }

    [Fact]
    public void MentionQueriesTranslateWithEmployeeJsonAndConversationScope()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var user = new User("SaleUser");
        var access = new InternalMailAreaAccessService(db, user);
        var mentions = access.MentionedMessages();
        var sql = mentions.ToQueryString();
        Assert.Contains("@>", sql);
        Assert.Contains("employeeId", sql);
        Assert.Contains("CompanyId", sql);
        Assert.Contains("IsDeleted", sql);
        Assert.Contains("InternalConversationParticipants", sql);
        Assert.Contains("ReplyToMessageId", sql);
        Assert.NotEmpty(access.MentionedHubEvents(access.Notifications(db.Notifications))
            .OrderByDescending(n => n.CreatedDate).Take(30).ToQueryString());
        Assert.NotEmpty(mentions.GroupBy(m => m.InternalConversationId).Select(g => new
        {
            Id = g.Key,
            MessageId = g.OrderByDescending(m => m.SentAt).ThenByDescending(m => m.InternalMessageId)
                .Select(m => m.InternalMessageId).First(),
            Unread = g.Count(m => m.ReadStates.Any(s => s.EmployeeId == user.EmployeeId && !s.IsRead))
        }).ToQueryString());
    }

    [Fact]
    public void MineIncludesRepliesWithoutTagButExcludesSelfDeletedAndOtherAuthors()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.Internal, true);
        var access = new InternalMailAreaAccessService(store.Db, user);
        var reply = store.Db.InternalMessages.Single();
        reply.SenderEmployeeId = Guid.NewGuid();
        reply.PayloadJson = null;
        reply.ReplyToMessage = new InternalMessage
        {
            InternalMessageId = Guid.NewGuid(),
            InternalConversationId = reply.InternalConversationId,
            SenderEmployeeId = user.EmployeeId!.Value
        };
        reply.ReplyToMessageId = reply.ReplyToMessage.InternalMessageId;
        Assert.Single(access.MentionedMessages());
        reply.IsDeleted = true;
        Assert.Empty(access.MentionedMessages());
        reply.IsDeleted = false;
        reply.SenderEmployeeId = user.EmployeeId.Value;
        Assert.Empty(access.MentionedMessages());
        reply.SenderEmployeeId = Guid.NewGuid();
        reply.ReplyToMessage.SenderEmployeeId = Guid.NewGuid();
        Assert.Empty(access.MentionedMessages());
        reply.ReplyToMessage.SenderEmployeeId = user.EmployeeId.Value;
        store.Child.Participants.Single().IsActive = false;
        Assert.Empty(access.MentionedMessages());
    }

    private static (IInternalMailDbContext Db, InternalConversation Root, InternalConversation Child) CreateStore(User user, InternalMailRelatedType type, bool member)
    {
        var root = new InternalConversation { InternalConversationId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, RelatedType = InternalMailRelatedType.Internal, IsActive = true };
        var child = new InternalConversation { InternalConversationId = Guid.NewGuid(), CompanyId = user.CompanyId.Value, RelatedType = type, RelatedId = root.InternalConversationId, IsActive = true };
        if (member) child.Participants.Add(new() { EmployeeId = user.EmployeeId!.Value, IsActive = true });
        var db = DispatchProxy.Create<IInternalMailDbContext, MemoryStore>();
        var proxy = (MemoryStore)(object)db;
        proxy.Values[nameof(db.QueryInternalMailNotificationLinks)] = new AsyncQuery<InternalMailNotificationLink>([]);
        proxy.Values[nameof(db.QueryInternalConversationNotificationUnreadCounts)] = new AsyncQuery<InternalConversationNotificationUnreadCount>([]);
        proxy.Values[nameof(db.InternalConversations)] = new MemorySet<InternalConversation>([root, child]);
        proxy.Values[nameof(db.InternalMessages)] = new MemorySet<InternalMessage>([new() { InternalMessageId = Guid.NewGuid(), InternalConversationId = child.InternalConversationId, Conversation = child }]);
        return (db, root, child);
    }

    public class MemoryStore : DispatchProxy
    {
        public Dictionary<string, object> Values { get; } = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == nameof(IInternalMailDbContext.QueryQuotationBadgeSuppressedNotificationIds))
            {
                IQueryable<T> Rows<T>(string name) where T : class => Values.TryGetValue(name, out var rows)
                    ? (IQueryable<T>)rows : Array.Empty<T>().AsQueryable();
                return new AsyncQuery<Guid>(QuotationBadgeNotificationQuery.Suppressed(
                    Rows<InternalConversation>(nameof(IInternalMailDbContext.InternalConversations)),
                    Rows<InternalMessage>(nameof(IInternalMailDbContext.InternalMessages)),
                    Rows<Notification>(nameof(IInternalMailDbContext.Notifications)),
                    Rows<NotificationUserState>(nameof(IInternalMailDbContext.NotificationUserStates)),
                    Rows<InternalMailNotificationLink>(nameof(IInternalMailDbContext.QueryInternalMailNotificationLinks)),
                    (Guid)args![0]!, (Guid)args[1]!));
            }
            if (method!.Name == nameof(IInternalMailDbContext.QueryLatestInternalMailBusinessEvents))
            {
                IQueryable<T> Rows<T>(string name) where T : class => Values.TryGetValue(name, out var rows)
                    ? (IQueryable<T>)rows : Array.Empty<T>().AsQueryable();
                var company = (Guid)args![0]!;
                var ids = (Guid[])args[1]!;
                var topics = (int[])args[2]!;
                var conversations = Rows<InternalConversation>(nameof(IInternalMailDbContext.InternalConversations));
                var events = from n in Rows<Notification>(nameof(IInternalMailDbContext.Notifications))
                    join link in Rows<InternalMailNotificationLink>(nameof(IInternalMailDbContext.QueryInternalMailNotificationLinks)) on n.Id equals link.NotificationId
                    join message in Rows<InternalMessage>(nameof(IInternalMailDbContext.InternalMessages))
                        on new { link.ConversationId, link.MessageId }
                        equals new { ConversationId = (Guid?)message.InternalConversationId, MessageId = (Guid?)message.InternalMessageId }
                    where n.CompanyId == company && topics.Contains((int)n.Topic) && !message.IsDeleted && ids.Contains(message.InternalConversationId) &&
                        conversations.Any(c => c.InternalConversationId == message.InternalConversationId && c.CompanyId == company)
                    select new InternalMailBusinessEventRow { Id = n.Id, ConversationId = message.InternalConversationId,
                        CreatedDate = n.CreatedDate, Topic = n.Topic };
                return new AsyncQuery<InternalMailBusinessEventRow>(events.GroupBy(e => e.ConversationId)
                    .Select(g => g.OrderByDescending(e => e.CreatedDate).ThenByDescending(e => e.Id).First()));
            }
            if (Values.TryGetValue(method!.Name, out var configured)) return configured;
            if (method!.Name.StartsWith("get_"))
            {
                var name = method.Name[4..];
                if (Values.TryGetValue(name, out var value)) return value;
                var type = method.ReturnType.GetGenericArguments()[0];
                return Activator.CreateInstance(typeof(MemorySet<>).MakeGenericType(type), [Array.CreateInstance(type, 0)]);
            }
            throw new NotSupportedException(method.Name);
        }
    }

    public class MemorySet<T>(IEnumerable<T> items) : DbSet<T>, IQueryable<T> where T : class
    {
        public override Microsoft.EntityFrameworkCore.Metadata.IEntityType EntityType => throw new NotSupportedException();
        private readonly IQueryable<T> _query = new AsyncQuery<T>(items);
        Type IQueryable.ElementType => typeof(T);
        Expression IQueryable.Expression => _query.Expression;
        IQueryProvider IQueryable.Provider => _query.Provider;
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => _query.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _query.GetEnumerator();
    }

    private sealed class AsyncQuery<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public AsyncQuery(IEnumerable<T> values) : base(values) { }
        public AsyncQuery(Expression expression) : base(expression) { }
        IQueryProvider IQueryable.Provider => new AsyncProvider(this);
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => new AsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
    }

    private sealed class AsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
    {
        public T Current => inner.Current;
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(inner.MoveNext());
        public ValueTask DisposeAsync() { inner.Dispose(); return ValueTask.CompletedTask; }
    }

    private sealed class AsyncProvider(IQueryProvider inner) : IAsyncQueryProvider
    {
        public IQueryable CreateQuery(Expression expression) => throw new NotSupportedException();
        public IQueryable<T> CreateQuery<T>(Expression expression) => new AsyncQuery<T>(expression);
        public object? Execute(Expression expression) => inner.Execute(expression);
        public T Execute<T>(Expression expression) => inner.Execute<T>(expression);
        public T ExecuteAsync<T>(Expression expression, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (expression is MethodCallExpression call && call.Method.Name == "ExecuteUpdate")
            {
                // This memory fixture supports only the genuinely empty update. Positive
                // mutations and affected-row semantics are tested on isolated PostgreSQL.
                Assert.Empty(inner.CreateQuery(call.Arguments[0]).Cast<object>());
                return (T)(object)Task.FromResult(0);
            }
            var value = inner.Execute(expression);
            return (T)typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(typeof(T).GetGenericArguments()[0]).Invoke(null, [value])!;
        }
    }

    private sealed class User(string role) : ICurrentUser
    {
        public bool IsAuthenticated { get; set; } = true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; init; } = Guid.NewGuid();
        public Guid? CompanyId { get; set; } = Guid.NewGuid();
        public string? UserName => "test";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [role];
        public IReadOnlyCollection<string> Permissions { get; set; } = [];
        public bool HasExplicitPermissionSet { get; set; }
        public bool IsInRole(string value) => value == role;
    }
}
