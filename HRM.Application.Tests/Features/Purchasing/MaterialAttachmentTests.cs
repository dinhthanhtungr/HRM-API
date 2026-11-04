using System.Reflection;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.FileStorage;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.Attachments.Services;
using HRM.Application.Features.Purchasing.Catalog.Services;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Enums.Attachment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HRM.Application.Tests.Features.Purchasing;

public sealed class MaterialAttachmentTests
{
    [Fact]
    public async Task UploadCreatesCollectionAndAudit_DeleteSoftly_AndReturnsScopedUrls()
    {
        var f = new Fixture();
        var result = await f.Service.UploadAsync(f.Material.MaterialId, AttachmentSlot.MaterialTds, [Pdf()], f.Material.UpdatedDate, default);
        Assert.NotNull(f.Material.AttachmentCollectionId);
        Assert.Single(f.Store.Added.OfType<AttachmentCollection>());
        var file = Assert.Single(result.Attachments);
        Assert.StartsWith($"/api/v1/purchasing/materials/{f.Material.MaterialId}/attachments/", file.ContentUrl);
        Assert.EndsWith("?mode=download", file.DownloadUrl);
        Assert.True(file.IsPdf);
        Assert.Equal(f.User.EmployeeId, f.Material.UpdatedBy);
        Assert.Equal(result.UpdatedDate, f.Material.UpdatedDate);
        Assert.True(f.Store.Transaction.Committed);
        var read = await f.Service.ContentAsync(f.Material.MaterialId, file.AttachmentId, default);
        await read.Stream.DisposeAsync();
        Assert.Equal(1, f.Files.Reads);
        var deleted = await f.Service.DeleteAsync(f.Material.MaterialId, file.AttachmentId, result.UpdatedDate, default);
        Assert.Empty(deleted.Attachments);
        Assert.False(Assert.Single(f.Store.Added.OfType<AttachmentModel>()).IsActive);
        Assert.True(deleted.UpdatedDate > result.UpdatedDate);
        Assert.Empty(f.Storage.Deleted);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task DeniesUnauthenticatedAndCatalogMissing(bool authenticated, bool catalog)
    {
        var f = new Fixture(authenticated, catalog);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.ListAsync(f.Material.MaterialId, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.UploadAsync(f.Material.MaterialId, AttachmentSlot.MaterialTds, [Pdf()], null, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.DeleteAsync(f.Material.MaterialId, Guid.NewGuid(), null, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.ContentAsync(f.Material.MaterialId, Guid.NewGuid(), default));
        Assert.Empty(f.Storage.Saved);
    }

    [Fact]
    public async Task CompanyInactiveVersionAndAttachmentOwnershipAreEnforced()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => f.Service.UploadAsync(f.Material.MaterialId, AttachmentSlot.MaterialTds, [Pdf()], null, default));
        Assert.Empty(f.Store.Added);
        Assert.Empty(f.Storage.Saved);
        var foreignFile = new AttachmentModel { AttachmentId = Guid.NewGuid(), AttachmentCollectionId = Guid.NewGuid(), IsActive = true };
        f.Store.Seed(nameof(f.Db.AttachmentModels), foreignFile);
        await Assert.ThrowsAsync<FileNotFoundException>(() => f.Service.ContentAsync(f.Material.MaterialId, foreignFile.AttachmentId, default));
        await Assert.ThrowsAsync<FileNotFoundException>(() => f.Service.DeleteAsync(f.Material.MaterialId, foreignFile.AttachmentId, f.Material.UpdatedDate, default));
        Assert.Equal(0, f.Files.Reads);
        Assert.True(foreignFile.IsActive);
        f.Material.IsActive = false;
        await Assert.ThrowsAsync<FileNotFoundException>(() => f.Service.ListAsync(f.Material.MaterialId, default));
        f.Material.IsActive = true;
        f.Material.CompanyId = Guid.NewGuid();
        await Assert.ThrowsAsync<FileNotFoundException>(() => f.Service.ListAsync(f.Material.MaterialId, default));
        await Assert.ThrowsAsync<FileNotFoundException>(() => f.Service.UploadAsync(f.Material.MaterialId, AttachmentSlot.MaterialTds, [Pdf()], f.Material.UpdatedDate, default));
    }

    [Fact]
    public async Task ForeignEmployeeCannotMutate_AndEmptyMaterialHasNoFiles()
    {
        var f = new Fixture();
        Assert.Empty((await f.Service.ListAsync(f.Material.MaterialId, default)).Attachments);
        f.Store.Seed(nameof(f.Db.Employees), new Employee { EmployeeId = f.User.EmployeeId!.Value, CompanyId = Guid.NewGuid(), IsActive = true });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.UploadAsync(f.Material.MaterialId, AttachmentSlot.MaterialTds, [Pdf()], f.Material.UpdatedDate, default));
        Assert.Empty(f.Storage.Saved);
    }

    [Fact]
    public async Task ReusesCollectionAndRejectsStaleVersionWithoutSavingMoreFiles()
    {
        var f = new Fixture();
        var collection = Guid.NewGuid();
        f.Material.AttachmentCollectionId = collection;
        f.Material.UpdatedDate = null;
        f.Store.Seed(nameof(f.Db.AttachmentCollections), new AttachmentCollection { AttachmentCollectionId = collection });
        var first = await f.Service.UploadAsync(f.Material.MaterialId, AttachmentSlot.MaterialTds, [Pdf()], null, default);
        Assert.Empty(f.Store.Added.OfType<AttachmentCollection>());
        Assert.Equal(collection, f.Material.AttachmentCollectionId);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            f.Service.UploadAsync(f.Material.MaterialId, AttachmentSlot.MaterialTds, [Pdf()], null, default));
        Assert.Single(f.Storage.Saved);
        var second = await f.Service.UploadAsync(f.Material.MaterialId, AttachmentSlot.MaterialTds, [Pdf()], first.UpdatedDate, default);
        Assert.Equal(2, second.Attachments.Count);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => f.Service.DeleteAsync(f.Material.MaterialId,
            first.Attachments[0].AttachmentId, first.UpdatedDate, default));
        Assert.All(f.Store.Added.OfType<AttachmentModel>(), x => Assert.True(x.IsActive));
    }

    [Fact]
    public async Task PhotoValidationChecksHeaderAndRestoresStreamBeforeStorage()
    {
        using var stream = new MemoryStream(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0 });
        await MaterialAttachmentUploadRules.ValidateAsync(AttachmentSlot.Photo,
            [new(stream, "image.png", "image/png", stream.Length)], default);
        Assert.Equal(0, stream.Position);
        await Assert.ThrowsAsync<InvalidOperationException>(() => MaterialAttachmentUploadRules.ValidateAsync(
            AttachmentSlot.Photo, [Pdf()], default));
    }

    [Fact]
    public async Task PartialStorageFailureRollsBackAndDeletesSavedFiles()
    {
        var f = new Fixture();
        f.Storage.FailOnSecondSave = true;
        await Assert.ThrowsAsync<IOException>(() => f.Service.UploadAsync(f.Material.MaterialId, AttachmentSlot.MaterialTds, [Pdf(), Pdf()], f.Material.UpdatedDate, default));
        Assert.True(f.Store.Transaction.RolledBack);
        Assert.Equal(f.Storage.Saved, f.Storage.Deleted);
    }

    [Fact]
    public async Task RejectsInvalidSlotSpoofedMimeEmptyAndOversizedFilesBeforeWriting()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.UploadAsync(f.Material.MaterialId, AttachmentSlot.InternalMail, [Pdf()], f.Material.UpdatedDate, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.UploadAsync(f.Material.MaterialId, AttachmentSlot.MaterialTds, [], f.Material.UpdatedDate, default));
        var fake = new AttachmentUploadFile(new MemoryStream("malicious content"u8.ToArray()), "fake.pdf", "application/pdf", 17);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.UploadAsync(f.Material.MaterialId, AttachmentSlot.MaterialTds, [fake], f.Material.UpdatedDate, default));
        var big = Pdf() with { Length = 51L * 1024 * 1024 };
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.UploadAsync(f.Material.MaterialId, AttachmentSlot.MaterialTds, [big], f.Material.UpdatedDate, default));
        Assert.Empty(f.Storage.Saved);
        Assert.Empty(f.Store.Added);
    }

    private static AttachmentUploadFile Pdf() => new(new MemoryStream("%PDF-1.7 sample"u8.ToArray()), "doc.pdf", "application/pdf", 15);

    private sealed class Fixture
    {
        internal readonly IPLMWriteDbContext Db = DispatchProxy.Create<IPLMWriteDbContext, PurchasingCatalogManagementTests.CatalogStore>();
        internal PurchasingCatalogManagementTests.CatalogStore Store => (PurchasingCatalogManagementTests.CatalogStore)Db;
        internal readonly IqcWorkflowTests.User User;
        internal readonly Material Material;
        internal readonly Storage Storage = new();
        internal readonly Files Files = new();
        internal readonly MaterialAttachmentService Service;
        internal Fixture(bool authenticated = true, bool catalog = true)
        {
            User = new() { IsAuthenticated = authenticated, HasExplicitPermissionSet = true,
                Permissions = catalog ? [ApplicationPermissions.Purchasing.ManageCatalog] : [ApplicationPermissions.Purchasing.ManagePurchaseOrders] };
            Material = new() { MaterialId = Guid.NewGuid(), CompanyId = User.CompanyId!.Value, IsActive = true, UpdatedDate = new(2026, 10, 20) };
            Store.ReflectAddedOnSave = true;
            Store.Seed(nameof(Db.Materials), Material);
            Store.Seed(nameof(Db.Employees), new Employee { EmployeeId = User.EmployeeId!.Value, CompanyId = User.CompanyId!.Value, IsActive = true });
            // Ensure fake store reflects new attachments on save.
            Store.Seed<AttachmentModel>(nameof(Db.AttachmentModels));
            Service = new(Db, User, new CurrentUserPermissionService(User), new Clock(), Files, Storage, NullLogger<MaterialAttachmentService>.Instance);
        }
    }

    private sealed class Clock : IDateTimeProvider
    {
        public DateTime Now => new(2026, 10, 21, 10, 0, 0);
        public DateTime UtcNow => Now;
    }

    private sealed class Storage : IFileStorage
    {
        internal List<string> Saved { get; } = [];
        internal List<string> Deleted { get; } = [];
        internal bool FailOnSecondSave;
        public Task<string> SaveAsync(Stream stream, string contentType, string fileName, string folder, CancellationToken ct = default)
        {
            if (FailOnSecondSave && Saved.Count == 1) throw new IOException("Simulated storage failure");
            var path = folder + "/" + Guid.NewGuid() + ".pdf";
            Saved.Add(path); return Task.FromResult(path);
        }
        public Task DeleteAsync(string path, CancellationToken ct = default) { Deleted.Add(path); return Task.CompletedTask; }
        public Task<StoredFile> OpenReadAsync(string path, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveAtPathAsync(Stream stream, string path, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class Files : IAttachmentService
    {
        internal int Reads;
        public Task<AttachmentContent> GetContentAsync(Guid id, CancellationToken ct = default)
        { Reads++; return Task.FromResult(new AttachmentContent(new MemoryStream(), "application/pdf", "doc.pdf", 0)); }
        public Task<IReadOnlyList<AttachmentDto>> UploadListAsync(Guid id, AttachmentSlot slot, IReadOnlyList<AttachmentUploadFile> files, Guid? by, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AttachmentDto>> ListAsync(Guid id, AttachmentSlot? slot, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task HardDeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
