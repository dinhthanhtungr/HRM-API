using System.Reflection;
using System.Text.Json;
using HRM.Application.Abstractions.FileStorage;
using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.DevAndQA.IQC.Dtos.EvaluationStandards;
using HRM.Application.Features.DevAndQA.IQC.Services.EvaluationStandards;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Attachments;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Services;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.Attachment;
using HRM.Domain.Enums.Devandqa;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

namespace HRM.Application.Tests.Features.DevAndQA;

public sealed class EvaluationFileSaveTests
{
    [Fact]
    public async Task GenericUploadCannotBypassEvaluationOwnerScope()
    {
        var generic = new HRM.Application.Features.Attachments.Services.AttachmentService(null!, null!, null!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => generic.UploadListAsync(Guid.NewGuid(),
            AttachmentSlot.EvaluationAttachment, [File()], null));
    }

    [Fact]
    public async Task ChildTransactionCannotCommitOrDisposeCombinedTransaction()
    {
        var parent = new IqcTestStore.TestTransaction();
        var type = typeof(HRM.Infrastructure.DatabaseContext.ApplicationDbs.ApplicationDbContext)
            .GetNestedType("BorrowedEvaluationTransaction", BindingFlags.NonPublic)!;
        var child = (Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction)Activator.CreateInstance(type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [parent], null)!;
        await child.CommitAsync();
        await child.DisposeAsync();
        Assert.False(parent.Committed);
        Assert.False(parent.RolledBack);
        await parent.CommitAsync();
        Assert.True(parent.Committed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task ConfigurationResourcesRejectFileAccess(int resource)
    {
        var (db, _) = IqcTestStore.CreateContext<IEvaluationFileSaveDbContext>();
        var user = new IqcWorkflowTests.User();
        var access = new EvaluationAccessService(db, user, new CurrentUserPermissionService(user));
        var resolver = new EvaluationFileOwnerResolver(db, access);
        await Assert.ThrowsAsync<EvaluationRequestException>(() => resolver.ResolveAsync(
            (EvaluationFileResource)resource, Guid.NewGuid(), false, default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CoaIssueRequiresActiveFileForRequiredAttachment(bool hasFile)
    {
        var (db, store) = IqcTestStore.CreateContext<IEvaluationDbContext>();
        var user = new IqcWorkflowTests.User();
        var collection = Guid.NewGuid();
        var row = new CoaCertificateResult { Id = Guid.NewGuid(), HasConfigurationSnapshot = true,
            EvaluationStandardId = Guid.NewGuid(), CriterionCode = "A", CriterionName = "Attachment",
            DataType = EvaluationDataType.Attachment, IsRequired = true,
            AttachmentCollectionId = hasFile ? collection : null };
        var certificate = new CoaCertificate { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            CertificateNo = "C", BatchNo = "B", Results = [row] };
        store.Seed("CoaCertificates", certificate);
        store.Seed("Employees", new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId });
        store.Seed("AttachmentModels", new AttachmentModel { AttachmentId = Guid.NewGuid(), AttachmentCollectionId = collection,
            Slot = AttachmentSlot.EvaluationAttachment, IsActive = hasFile });
        var fixture = new EvaluationServicesFixture(db, user, new CurrentUserPermissionService(user), new());
        if (hasFile)
            Assert.Equal(CoaCertificateStatus.Issued, (await fixture.Coa.IssueCoaAsync(certificate.Id, default)).Status);
        else
        {
            await Assert.ThrowsAsync<EvaluationRequestException>(() => fixture.Coa.IssueCoaAsync(certificate.Id, default));
            Assert.False(store.Transaction.Committed);
        }
    }

    [Fact]
    public void AttachmentTypeRejectsTextValueAndInvalidConfiguration()
    {
        Assert.Equal(8, (int)EvaluationDataType.Attachment);
        Assert.NotNull(EvaluationStandardDefaultsRules.Validate(EvaluationDataType.Attachment, null, "{\"maxFiles\":0}", null));
        Assert.NotNull(EvaluationStandardDefaultsRules.Validate(EvaluationDataType.Attachment, null, "{\"allowedExtensions\":[\".exe\"]}", null));
        Assert.NotNull(EvaluationStandardDefaultsRules.Validate(EvaluationDataType.Attachment, null, null, "{\"equals\":\"x\"}"));
        Assert.Null(EvaluationStandardDefaultsRules.Validate(EvaluationDataType.Attachment, null, "{\"maxFiles\":2,\"allowedExtensions\":[\".pdf\"]}", null));
        var config = new HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos.EvaluationConfigurationDto(EvaluationDataType.Attachment, null, null, null, null, null, null, null, false, 1);
        Assert.Null(EvaluationValueRules.Normalize(null, config));
        Assert.Throws<EvaluationRequestException>(() => EvaluationValueRules.Normalize("file-id", config));
        Assert.Throws<EvaluationRequestException>(() => EvaluationAttachmentRules.Validate(config with { DataType = EvaluationDataType.Text }, [File()], 0));
        Assert.Throws<EvaluationRequestException>(() => EvaluationAttachmentRules.Validate(config, [File() with { ContentType = "application/x-msdownload" }], 0));
        Assert.Throws<EvaluationRequestException>(() => EvaluationAttachmentRules.Validate(config, [File()], 20));
    }

    [Fact]
    public void MultipartManifestRequiresEveryFileExactlyOnce()
    {
        var uploads = new[] { new EvaluationNamedUpload("f1", File()) };
        EvaluationFileSaveService.ValidateManifest([new(null, ["f1"])], uploads);
        Assert.Throws<EvaluationRequestException>(() => EvaluationFileSaveService.ValidateManifest([], uploads));
        Assert.Throws<EvaluationRequestException>(() => EvaluationFileSaveService.ValidateManifest([new(null, ["f1", "f1"])], uploads));
        Assert.Throws<EvaluationRequestException>(() => EvaluationFileSaveService.ValidateManifest([new(null, ["other"])], uploads));
    }

    [Theory]
    [InlineData("KHOUser", true)]
    [InlineData("SaleUser", true)]
    [InlineData("QCUser", false)]
    public async Task FileSaveFailsClosedWithoutManageOrAuthentication(string role, bool authenticated)
    {
        var user = new IqcWorkflowTests.User { Roles = [role], IsAuthenticated = authenticated };
        var access = new EvaluationAccessService(null!, user, new CurrentUserPermissionService(user));
        var service = new EvaluationFileSaveService(null!, null!, access, null!, null!, NullLogger<EvaluationFileSaveService>.Instance);
        var error = await Assert.ThrowsAsync<EvaluationRequestException>(() => service.SaveAsync(EvaluationFileResource.Coa,
            Guid.NewGuid().ToString(), false, Envelope(), [new("f1", File())], default));
        Assert.Equal(403, error.StatusCode);
    }

    [Fact]
    public async Task OneSaveCreatesCollectionAndFilesAndCommits()
    {
        var (service, store, standard, storage) = Fixture();
        var result = await service.SaveAsync(EvaluationFileResource.Coa, standard.CoaCertificateId.ToString(), false, Envelope(), [new("f1", File())], default);
        Assert.True(store.Transaction.Committed);
        Assert.NotNull(standard.AttachmentCollectionId);
        Assert.Equal(standard.AttachmentCollectionId, Assert.Single(Assert.IsType<HRM.Application.Features.DevAndQA.CoaCertificates.Dtos.CoaCertificateDto>(result.Record).Results).AttachmentCollectionId);
        var file = Assert.IsType<AttachmentModel>(Assert.Single(store.Added.OfType<AttachmentModel>()));
        Assert.Equal(AttachmentSlot.EvaluationAttachment, file.Slot);
        Assert.Equal(standard.AttachmentCollectionId, file.AttachmentCollectionId);
        Assert.Equal(file.AttachmentId, Assert.Single(Assert.Single(result.Attachments).Files).AttachmentId);
        Assert.Empty(storage.Deleted);
    }

    [Fact]
    public async Task FailedSecondFileRollsBackAndCleansFirstFile()
    {
        var (service, store, standard, storage) = Fixture();
        storage.FailAt = 2;
        var request = Envelope(["f1", "f2"]);
        await Assert.ThrowsAsync<IOException>(() => service.SaveAsync(EvaluationFileResource.Coa, standard.CoaCertificateId.ToString(), false,
            request, [new("f1", File()), new("f2", File())], default));
        Assert.False(store.Transaction.Committed);
        Assert.True(store.Transaction.RolledBack);
        Assert.Equal(2, storage.Deleted.Count); // Dọn cả file viết dở của lần upload lỗi.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WrongTypeOrForeignCompanyFailsBeforeFileStorage(bool foreign)
    {
        var (service, store, standard, storage) = Fixture();
        if (foreign) standard.CoaCertificate.CompanyId = Guid.NewGuid(); else standard.DataType = EvaluationDataType.Decimal;
        var error = await Assert.ThrowsAsync<EvaluationRequestException>(() => service.SaveAsync(EvaluationFileResource.Coa,
            standard.CoaCertificateId.ToString(), false, Envelope(), [new("f1", File())], default));
        Assert.Equal(foreign ? 404 : 400, error.StatusCode);
        Assert.False(store.Transaction.Committed);
        Assert.Equal(0, storage.Saves);
    }

    private static readonly Guid StandardId = Guid.NewGuid();

    private static AttachmentUploadFile File() => new(new MemoryStream([1, 2, 3]), "evidence.txt", "text/plain", 3);
    private static EvaluationFileSaveEnvelope Envelope(string[]? keys = null) =>
        new(JsonSerializer.SerializeToElement(new { }), [new(StandardId, keys ?? ["f1"])]);

    private static (EvaluationFileSaveService Service, IqcTestStore Store, CoaCertificateResult Standard, Storage Storage) Fixture()
    {
        var (db, store) = IqcTestStore.CreateContext<IEvaluationFileSaveDbContext>();
        store.ReflectAddedOnSave = true;
        var user = new IqcWorkflowTests.User();
        var standard = new CoaCertificateResult { Id = Guid.NewGuid(), CoaCertificateId = Guid.NewGuid(),
            EvaluationStandardId = StandardId, CriterionCode = "A", CriterionName = "Attachment",
            HasConfigurationSnapshot = true, DataType = EvaluationDataType.Attachment };
        var certificate = new CoaCertificate { Id = standard.CoaCertificateId, CompanyId = user.CompanyId!.Value,
            CertificateNo = "C", ProductCode = "P", ProductName = "Product", BatchNo = "B", Results = [standard] };
        standard.CoaCertificate = certificate;
        store.Seed("CoaCertificates", certificate);
        store.Seed("CoaCertificateResults", standard);
        store.Seed("Employees", new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId });
        var access = new EvaluationAccessService(db, user, new CurrentUserPermissionService(user));
        var sender = DispatchProxy.Create<ISender, Sender>();
        ((Sender)(object)sender).Result = new(certificate.Id, "C", "P", "Product", "B", null, null,
            CoaCertificateStatus.Draft, null, null, null, null, null, user.EmployeeId.Value, DateTime.Now, null, null,
            [HRM.Application.Features.DevAndQA.CoaCertificates.Services.CoaCertificateService.ResultDto(standard)]);
        var storage = new Storage();
        return (new(db, sender, access, new(db, access), storage, NullLogger<EvaluationFileSaveService>.Instance), store, standard, storage);
    }

    public class Sender : DispatchProxy
    {
        public HRM.Application.Features.DevAndQA.CoaCertificates.Dtos.CoaCertificateDto Result = null!;
        protected override object Invoke(MethodInfo? method, object?[]? args) => Task.FromResult(Result);
    }
    private sealed class Storage : IFileStorage
    {
        public int Saves;
        public int FailAt;
        public List<string> Deleted = [];
        public Task<string> SaveAsync(Stream stream, string type, string name, string folder, CancellationToken ct = default)
        {
            Saves++;
            if (Saves == FailAt) throw new IOException("Simulated storage failure");
            return Task.FromResult(folder + "/" + Saves + ".txt");
        }
        public Task SaveAtPathAsync(Stream stream, string path, CancellationToken ct = default)
        {
            Saves++;
            if (Saves == FailAt) throw new IOException("Simulated partial write failure");
            return Task.CompletedTask;
        }
        public Task<StoredFile> OpenReadAsync(string path, CancellationToken ct = default) => Task.FromResult(new StoredFile(new MemoryStream([1, 2, 3]), "text/plain", "evidence.txt", 3));
        public Task DeleteAsync(string path, CancellationToken ct = default) { Deleted.Add(path); return Task.CompletedTask; }
    }
}
