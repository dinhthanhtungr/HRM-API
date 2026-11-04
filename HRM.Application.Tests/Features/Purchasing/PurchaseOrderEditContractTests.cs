using System.Reflection;
using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Purchasing.PurchaseOrders.Commands.UpdatePurchaseOrderNotes;
using HRM.Application.Features.Purchasing.PurchaseOrders.Commands.CreatePurchaseOrder;
using HRM.Application.Features.Purchasing.PurchaseOrders.Documents;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.Orders;
using Xunit;

namespace HRM.Application.Tests.Features.Purchasing;

public sealed class PurchaseOrderEditContractTests
{
    [Theory]
    [InlineData("Pending")]
    [InlineData("InProgress")]
    [InlineData("Completed")]
    public async Task ClientDeliveryDateAndClearAreIgnoredWithoutSaving(string status)
    {
        var f = new Fixture(status);
        var existing = new DateTime(2020, 1, 1);
        f.Order.RealDeliveryDate = existing;
        Assert.True((await f.Patch(new() { RealDeliveryDate = DateTime.MaxValue,
            ClearFields = ["REALDELIVERYDATE"] })).Success);
        Assert.Equal(existing, f.Order.RealDeliveryDate);
        Assert.Equal(0, f.Store.Saves);
        Assert.True((await f.Patch(new() { PlpuComment = "note", RealDeliveryDate = DateTime.MaxValue,
            ClearFields = ["realDeliveryDate"] })).Success);
        Assert.Equal(existing, f.Order.RealDeliveryDate);
        Assert.Equal("note", f.Order.PLPUComment);
    }

    [Fact]
    public async Task CreateRejectsMissingPriceRequiredHeadersAndEmptyReferencesBeforeWriting()
    {
        var user = new IqcWorkflowTests.User { Roles = ["Purchaser"] };
        var handler = new CreatePurchaseOrderCommandHandler(null!, user, new CurrentUserPermissionService(user), null!, null!);
        var valid = new CreatePurchaseOrderRequest { SupplierId = Guid.NewGuid(), OrderType = "NCC", RequestDeliveryDate = new(2020, 1, 1),
            Items = [new() { MaterialId = Guid.NewGuid(), Quantity = 1m, UnitPriceAgreed = 0m }] };
        foreach (var request in new[] { valid with { OrderType = null }, valid with { OrderType = "PO" },
            valid with { RequestDeliveryDate = null }, valid with { MerchandiseOrderIds = [Guid.Empty] },
            valid with { Items = [new() { MaterialId = Guid.NewGuid(), Quantity = 1m }] } })
            Assert.False((await handler.Handle(new(request), default)).Success);
    }
    [Theory]
    [InlineData("Pending", true)]
    [InlineData("InProgress", false)]
    [InlineData("Completed", false)]
    [InlineData("Canceled", false)]
    [InlineData("ExchangeRequested", false)]
    [InlineData("Unknown", false)]
    public async Task PatchAndAllDocumentMutationsUseTheSameStateGate(string status, bool editable)
    {
        var f = new Fixture(status);
        var result = await f.Patch(new() { Comment = "new" });
        Assert.Equal(editable, result.Success);
        Assert.Equal(editable ? "new" : "old", f.Order.Comment);
        if (editable) return;
        var documents = new PurchaseOrderDocumentService(f.Db, null!, f.User, new CurrentUserPermissionService(f.User));
        var id = Guid.NewGuid();
        var writes = new Func<Task>[]
        {
            async () => await documents.CreateAsync(f.Order.PurchaseOrderId, new() { DocumentType = PurchaseOrderDocumentType.Contract }),
            async () => await documents.UpdateAsync(f.Order.PurchaseOrderId, id, new()),
            async () => await documents.DeleteAsync(f.Order.PurchaseOrderId, id),
            async () => await documents.UploadAttachmentsAsync(f.Order.PurchaseOrderId, id, []),
            async () => await documents.UploadAttachmentsByTypeAsync(f.Order.PurchaseOrderId, PurchaseOrderDocumentType.Contract, []),
            async () => await documents.DeleteAttachmentAsync(f.Order.PurchaseOrderId, id, id),
            async () => await documents.HardDeleteAttachmentAsync(f.Order.PurchaseOrderId, id, id)
        };
        foreach (var write in writes) await Assert.ThrowsAsync<PurchaseOrderWriteException>(write);
        Assert.Equal(0, f.Store.Saves);
    }

    [Fact]
    public async Task PatchClearsNullableFieldsAndRecalculatesTotalWithoutChangingLinesOrSupplier()
    {
        var f = new Fixture();
        var material = Guid.NewGuid();
        f.Order.PurchaseOrderDetails = [new() { MaterialId = material, IsActive = true, RequestQuantity = 2, UnitPriceAgreed = 100, TotalPriceAgreed = 200 }];
        var result = await f.Patch(new() { OrderType = "KH", RequestDeliveryDate = new(2020, 1, 1),
            PaymentTypes = " Cash ", DeliveryAddress = " Other warehouse ", Vat = 8 });
        Assert.True(result.Success, result.Message);
        Assert.Equal("KH", f.Order.OrderType);
        Assert.Equal(216m, f.Order.PurchaseOrderSnapshot!.TotalPrice);
        Assert.Equal("Cash", f.Order.PurchaseOrderSnapshot.PaymentTypes);
        Assert.Equal(material, Assert.Single(f.Order.PurchaseOrderDetails).MaterialId);
        Assert.True((await f.Patch(new() { ClearFields = ["vat", "deliveryAddress", "realDeliveryDate", "comment"] })).Success);
        Assert.Null(f.Order.PurchaseOrderSnapshot.Vat);
        Assert.Null(f.Order.PurchaseOrderSnapshot.DeliveryAddress);
        Assert.Null(f.Order.RealDeliveryDate);
        Assert.Null(f.Order.Comment);
        Assert.Equal(200m, f.Order.PurchaseOrderSnapshot.TotalPrice);
    }

    [Fact]
    public async Task InvalidPayloadAndNoChangeAreRejectedWithoutSaving()
    {
        var f = new Fixture();
        foreach (var request in new UpdatePurchaseOrderNotesRequest[]
        {
            new(), new() { Vat = -1 }, new() { Vat = 101 }, new() { OrderType = "PO" },
            new() { ClearFields = ["orderType"] }, new() { ClearFields = ["requestDeliveryDate"] },
            new() { Vat = 0, ClearFields = ["vat"] }, new() { Comment = "old" },
            new() { IsActive = false, Vat = 8 }
        }) Assert.False((await f.Patch(request)).Success);
        Assert.Equal(0, f.Store.Saves);
        Assert.Equal("old", f.Order.Comment);
    }

    [Fact]
    public async Task StatusChangedAtLockIsRecheckedAndInactiveCannotRestore()
    {
        var f = new Fixture();
        f.Store.OnLock = () => f.Order.Status = "InProgress";
        Assert.False((await f.Patch(new() { Comment = "racing update" })).Success);
        Assert.Equal("old", f.Order.Comment);
        Assert.Equal(0, f.Store.Saves);
        f.Store.OnLock = null;
        f.Order.IsActive = false;
        Assert.False((await f.Patch(new() { IsActive = true, Comment = "restore" })).Success);
        Assert.False(f.Order.IsActive);
    }

    [Theory]
    [InlineData("Pending", true)]
    [InlineData("InProgress", true)]
    [InlineData("Completed", true)]
    [InlineData("Canceled", false)]
    [InlineData("ExchangeRequested", false)]
    [InlineData("Unknown", false)]
    public async Task InternalNotesCanChangeDuringAndAfterDeliveryWithoutUnlockingOtherFields(string status, bool allowed)
    {
        var f = new Fixture(status);
        Assert.Equal(allowed, (await f.Patch(new() { PlpuComment = "Internal note" })).Success);
        if (!allowed) return;
        Assert.Equal(status == "Pending", (await f.Patch(new() { PlpuComment = "mixed", Comment = "external" })).Success);
        Assert.True((await f.Patch(new() { ClearFields = ["plpuComment"] })).Success);
        Assert.Null(f.Order.PLPUComment);
        Assert.Equal(status == "Pending", (await f.Patch(new() { PlpuComment = "mixed", Vat = 8 })).Success);
        f.Order.IsActive = false;
        Assert.False((await f.Patch(new() { PlpuComment = "inactive" })).Success);
    }

    [Theory]
    [InlineData("SaleUser", false)]
    [InlineData("Purchaser", true)]
    public async Task PermissionsEmployeeAndCompanyRemainEnforced(string role, bool allowed)
    {
        var f = new Fixture();
        f.User = new IqcWorkflowTests.User { Roles = [role], CompanyId = f.User.CompanyId, EmployeeId = f.User.EmployeeId };
        Assert.Equal(allowed, (await f.Patch(new() { Comment = "allowed" })).Success);
        f.Store.Seed<Employee>(nameof(f.Db.Employees));
        Assert.False((await f.Patch(new() { Comment = "invalid employee" })).Success);
        f.Store.Seed(nameof(f.Db.Employees), new Employee { EmployeeId = f.User.EmployeeId!.Value, CompanyId = f.User.CompanyId });
        f.Order.CompanyId = Guid.NewGuid();
        Assert.False((await f.Patch(new() { Comment = "foreign" })).Success);
    }

    [Fact]
    public async Task InactiveOrderDocumentsRemainReadableButMutationsAreRejected()
    {
        var f = new Fixture();
        f.Order.IsActive = false;
        var document = new PurchaseOrderDocument { PurchaseOrderDocumentId = Guid.NewGuid(),
            PurchaseOrderId = f.Order.PurchaseOrderId, PurchaseOrder = f.Order, IsActive = true };
        f.Store.Seed(nameof(f.Db.PurchaseOrderDocuments), document);
        var service = new PurchaseOrderDocumentService(f.Db, null!, f.User, new CurrentUserPermissionService(f.User));
        Assert.True((await service.GetByIdAsync(f.Order.PurchaseOrderId, document.PurchaseOrderDocumentId)).Success);
        await Assert.ThrowsAsync<PurchaseOrderWriteException>(() => service.DeleteAsync(f.Order.PurchaseOrderId, document.PurchaseOrderDocumentId));
        Assert.True(document.IsActive);
    }

    private sealed class Fixture
    {
        public IqcWorkflowTests.User User = new() { Roles = ["Purchaser"] };
        public IPurchaseOrderDbContext Db { get; }
        public EditStore Store { get; }
        public PurchaseOrder Order { get; }
        public Fixture(string status = "Pending")
        {
            Db = DispatchProxy.Create<IPurchaseOrderDbContext, EditStore>(); Store = (EditStore)Db;
            Order = new() { PurchaseOrderId = Guid.NewGuid(), CompanyId = User.CompanyId, IsActive = true,
                Status = status, OrderType = "NCC", Comment = "old", PurchaseOrderSnapshot = new() { Vat = null } };
            Store.Seed(nameof(Db.PurchaseOrders), Order);
            Store.Seed(nameof(Db.Employees), new Employee { EmployeeId = User.EmployeeId!.Value, CompanyId = User.CompanyId });
        }
        public Task<HRM.Application.Commons.Models.OperationResult> Patch(UpdatePurchaseOrderNotesRequest request) =>
            new UpdatePurchaseOrderNotesCommandHandler(null!, Db, User, new CurrentUserPermissionService(User), new Clock())
                .Handle(new(Order.PurchaseOrderId, request), default);
    }
    public class EditStore : IqcTestStore
    {
        public Action? OnLock { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name == "LockPurchaseOrderAsync") { OnLock?.Invoke(); return Task.CompletedTask; }
            return base.Invoke(method, args);
        }
    }
    private sealed class Clock : IDateTimeProvider { public DateTime Now => new(2026, 10, 16); }
}
