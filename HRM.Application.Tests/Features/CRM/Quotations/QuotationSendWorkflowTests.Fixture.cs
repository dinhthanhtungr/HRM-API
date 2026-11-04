using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.CRM.Quotations.Commands.SaveQuotation;
using HRM.Application.Features.CRM.Quotations.Commands.UpdateQuotationCustomerPriceTiers;
using System.Reflection;
using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed partial class QuotationSendWorkflowTests
{
    private sealed class Fixture
    {
        public DateTime Now { get; } = new(2026, 10, 17, 10, 0, 0);
        public Quotation Quote { get; }
        public ProductPricingVersion Standard { get; }
        public InternalConversation Conversation { get; }
        public QuotationSendService Service { get; }
        public ICRMReadDbContext ReadDb { get; }
        public IqcTestStore ReadStore { get; }
        public ICRMWriteDbContext WriteDb { get; }
        public ICustomerVisibilityService Visibility { get; }
        public IDateTimeProvider Clock { get; }
        public KeyedMutationLock<Guid> MutationLock { get; } = new();
        public SaveQuotationCommandHandler SaveHandler() => new(ReadDb, WriteDb, Visibility,
            new QuotationHeaderUpdateService(ReadDb, WriteDb, Visibility, Clock), null!,
            new QuotationCustomerPriceUpdateService(ReadDb, WriteDb, Visibility, Clock),
            new QuotationAtomicSaveService(WriteDb), Service, MutationLock, Clock, new QuotationStandardReferenceSaveService(ReadDb));
        public UpdateQuotationCustomerPriceTiersCommandHandler PriceHandler() => new(
            new QuotationCustomerPriceUpdateService(ReadDb, WriteDb, Visibility, Clock), MutationLock,
            new QuotationAtomicSaveService(WriteDb), Service);
        public QuotationStore WriteStore { get; }
        public IqcTestStore MailStore { get; }
        public List<PublishNotificationRequest> Published { get; } = [];

        public Fixture(string role = "SaleUser")
        {
            var user = new IqcWorkflowTests.User { Roles = [role] };
            var read = IqcTestStore.CreateContext<ICRMReadDbContext>();
            ReadStore = read.Store;
            var write = DispatchProxy.Create<ICRMWriteDbContext, QuotationStore>();
            WriteStore = (QuotationStore)(object)write;
            var mail = IqcTestStore.CreateContext<IInternalMailDbContext>();
            MailStore = mail.Store;
            var tier = new QuotationLinePriceTier { MinQuantity = 350, MaxQuantity = 600 };
            tier.SetPrices(80, 10);
            Quote = new Quotation
            {
                QuotationId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, CustomerId = Guid.NewGuid(),
                SaleEmployeeId = user.EmployeeId!.Value, Note = "Existing quotation note",
                Customer = new Customer { CustomerName = "Customer" },
                Lines = [new QuotationLine { QuotationLineId = Guid.NewGuid(), Quantity = 400, ProductId = Guid.NewGuid(), ProductExternalIdSnapshot = "P1", PriceTiers = [tier] }]
            };
            Standard = new ProductPricingVersion
            {
                ProductPricingVersionId = Guid.NewGuid(), ProductId = Quote.Lines.Single().ProductId,
                CompanyId = Quote.CompanyId, ApprovedAt = Now.AddDays(-1), Status = ProductPricingStatus.Approved,
                PriceTiers = [new ProductPricingTier { MinQuantity = 300, MaxQuantity = 600, UnitPrice = 100 }]
            };
            Conversation = new InternalConversation
            {
                InternalConversationId = Guid.NewGuid(), CompanyId = Quote.CompanyId,
                RelatedType = InternalMailRelatedType.Quotation, RelatedId = Quote.QuotationId, IsActive = true
            };
            WriteStore.Seed(nameof(write.Quotations), Quote);
            read.Store.Seed(nameof(read.Db.Customers), new Customer { CustomerId = Quote.CustomerId, CompanyId = Quote.CompanyId });
            read.Store.Seed(nameof(read.Db.ProductPricingVersions), Standard);
            MailStore.Seed(nameof(mail.Db.InternalConversations), Conversation);
            var visibility = Stub<ICustomerVisibilityService>( (method, args) => method == "BuildScopeAsync"
                ? Task.FromResult(new ViewerScope(user.CompanyId.Value, user.EmployeeId.Value, true, true, new HashSet<Guid>(), new HashSet<Guid>(), Now))
                : method == "ApplyCustomerVisibility" ? args![0] : throw new NotSupportedException(method));
            var recipients = Stub<ISaleGroupRecipientResolver>((_, _) => Task.FromResult<IReadOnlyCollection<Guid>>([Guid.NewGuid()]));
            var notifications = Stub<INotificationService>((method, args) =>
            {
                Published.Add((PublishNotificationRequest)args![0]!);
                return Task.FromResult(Guid.NewGuid());
            });
            var clock = Stub<IDateTimeProvider>((_, _) => Now);
            ReadDb = read.Db; WriteDb = write; Visibility = visibility; Clock = clock;
            Service = new(read.Db, write, mail.Db, visibility, recipients, notifications, clock,
                MutationLock, new CurrentUserPermissionService(user), Options.Create(new QuotationFeatureOptions()));
        }

        public InternalMessage SeedPending()
        {
            Quote.Status = QuotationStatus.PendingSendConfirmation;
            Quote.UpdatedDate = Now;
            var message = new InternalMessage
            {
                InternalMessageId = Guid.NewGuid(), InternalConversationId = Conversation.InternalConversationId,
                Conversation = Conversation, SenderEmployeeId = Quote.SaleEmployeeId, IsUrgent = true, MessageType = InternalMessageType.Action
            };
            message.PayloadJson = JsonSerializer.Serialize(new QuotationSendConfirmationPayload
            {
                QuotationId = Quote.QuotationId, RequestMessageId = message.InternalMessageId,
                ConversationId = Conversation.InternalConversationId, ExpectedUpdatedDate = Now,
                ComparisonRuleVersion = QuotationSendConfirmationPayload.CurrentComparisonRuleVersion,
                PricingEvaluatedAt = Now,
                CommercialFingerprintVersion = 1,
                CommercialFingerprint = QuotationConfirmationCommercialRules.Fingerprint(Quote),
                Comparison = QuotationSendPriceComparison.Compare(Quote.Lines.Single(), Standard)
                    .Select(x => x with { StandardValidUntil = Standard.ApprovedAt!.Value.AddDays(30) }).ToArray()
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            MailStore.Seed("InternalMessages", message);
            return message;
        }
    }

    private static T Stub<T>(Func<string, object?[]?, object?> call) where T : class
    {
        var result = DispatchProxy.Create<T, MethodStub>();
        ((MethodStub)(object)result).Call = call;
        return result;
    }

    public class MethodStub : DispatchProxy
    {
        public Func<string, object?[]?, object?> Call { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Call(targetMethod!.Name, args);
    }

    public class QuotationStore : IqcTestStore
    {
        public bool FailWithConcurrency { get; set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => targetMethod!.Name switch
            {
                "SaveChangesAsync" when FailWithConcurrency => Task.FromException<int>(new DbUpdateConcurrencyException()),
                "BeginQuotationTransactionAsync" => Task.FromResult<IDbContextTransaction>(Transaction),
                "ClearTrackedChanges" => null,
                _ => base.Invoke(targetMethod, args)
            };
    }
}
