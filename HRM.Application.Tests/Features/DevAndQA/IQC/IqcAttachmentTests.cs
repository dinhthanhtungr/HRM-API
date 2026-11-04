using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.Attachments.Services;
using HRM.Application.Features.DevAndQA.IQC.Services.Attachments;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.Attachment;
using Xunit;

namespace HRM.Application.Tests.Features.DevAndQA.IQC;

public sealed class IqcAttachmentTests
{
    [Theory]
    [InlineData("SaleUser")]
    [InlineData("KHOUser")]
    public async Task MutationRequiresManageBeforeStorage(string role)
    {
        var user = new IqcWorkflowTests.User { Roles = [role] };
        var storage = new FakeAttachments();
        var service = new IqcAttachmentService(null!, user, new CurrentUserPermissionService(user), storage, new());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteAsync(1, Guid.NewGuid(), default));
        Assert.Equal(0, storage.Calls);
    }

    [Fact]
    public async Task UploadSavesStatusAndParentScopedUrls()
    {
        var (service, storage, qc) = Fixture();
        var files = await service.UploadAsync(1, AttachmentSlot.QcReport,
            [new(new MemoryStream([1]), "photo.png", "image/png", 1)], default);
        Assert.Equal(AttachmentUploadStatus.Ok, qc.AttachmentStatus);
        Assert.Equal(qc.AttachmentCollectionId, storage.UploadCollection);
        Assert.StartsWith("/api/v1/dev-and-qa/iqc/1/attachments/", files[0].Url);
        Assert.EndsWith("/download", files[0].DownloadUrl);
    }

    [Fact]
    public async Task FileFromAnotherCollectionCannotBeReadOrDeleted()
    {
        var (service, storage, _) = Fixture();
        await Assert.ThrowsAsync<FileNotFoundException>(() => service.GetContentAsync(1, Guid.NewGuid(), default));
        await Assert.ThrowsAsync<FileNotFoundException>(() => service.DeleteAsync(1, Guid.NewGuid(), default));
        Assert.Equal(0, storage.Calls);
    }

    [Fact]
    public async Task CrossCompanyParentIsHiddenBeforeStorage()
    {
        var (service, storage, qc) = Fixture();
        qc.WarehouseVoucherDetail.Voucher.CompanyId = Guid.NewGuid();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ListAsync(1, null, default));
        Assert.Equal(0, storage.Calls);
    }

    [Fact]
    public async Task InvalidExtensionAndSlotAreRejectedBeforeStorage()
    {
        var (service, storage, _) = Fixture();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(1, AttachmentSlot.QcReport,
            [new(new MemoryStream([1]), "file.exe", "image/png", 1)], default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ListAsync(1, AttachmentSlot.InternalMail, default));
        Assert.Equal(0, storage.Calls);
    }

    [Fact]
    public async Task DeletingLastFileClearsUploadStatus()
    {
        var (service, storage, qc) = Fixture();
        await service.DeleteAsync(1, storage.Id, default);
        Assert.Equal(AttachmentUploadStatus.None, qc.AttachmentStatus);
        Assert.True(storage.Deleted);
    }

    [Fact]
    public async Task UnauthenticatedReadFailsBeforeDatabase()
    {
        var user = new IqcWorkflowTests.User { IsAuthenticated = false };
        var storage = new FakeAttachments();
        var service = new IqcAttachmentService(null!, user, new CurrentUserPermissionService(user), storage, new());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ListAsync(1, null, default));
        Assert.Equal(0, storage.Calls);
    }

    private static (IqcAttachmentService Service, FakeAttachments Storage, QCInputByQC Qc) Fixture()
    {
        var (db, store) = IqcTestStore.CreateStore();
        var user = new IqcWorkflowTests.User();
        var detail = new WarehouseVoucherDetail { VoucherDetailId = 1,
            Voucher = new WarehouseVoucher { CompanyId = user.CompanyId!.Value } };
        var qc = new QCInputByQC { QCInputByQCId = Guid.NewGuid(), VoucherDetailId = 1,
            WarehouseVoucherDetail = detail, AttachmentCollectionId = Guid.NewGuid() };
        var storage = new FakeAttachments();
        store.Seed(nameof(db.WarehouseVoucherDetails), detail);
        store.Seed(nameof(db.QCInputByQCs), qc);
        store.Seed(nameof(db.Employees), new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId.Value });
        store.Seed(nameof(db.AttachmentModels), new AttachmentModel { AttachmentId = storage.Id,
            AttachmentCollectionId = qc.AttachmentCollectionId.Value, IsActive = true, Slot = AttachmentSlot.QcReport });
        return (new(db, user, new CurrentUserPermissionService(user), storage, new KeyedMutationLock<string>()), storage, qc);
    }

    private sealed class FakeAttachments : IAttachmentService
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid UploadCollection { get; private set; }
        public int Calls { get; private set; }
        public bool Deleted { get; private set; }
        public Task<IReadOnlyList<AttachmentDto>> UploadListAsync(Guid collectionId, AttachmentSlot slot,
            IReadOnlyList<AttachmentUploadFile> files, Guid? createdBy, CancellationToken cancellationToken = default)
        {
            Calls++; UploadCollection = collectionId;
            return Task.FromResult<IReadOnlyList<AttachmentDto>>([new() { AttachmentId = Id, Slot = slot }]);
        }
        public Task<IReadOnlyList<AttachmentDto>> ListAsync(Guid collectionId, AttachmentSlot? slot, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult<IReadOnlyList<AttachmentDto>>([]); }
        public Task<AttachmentContent> GetContentAsync(Guid id, CancellationToken cancellationToken = default)
        { Calls++; throw new NotSupportedException(); }
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        { Calls++; Deleted = true; return Task.CompletedTask; }
        public Task HardDeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
