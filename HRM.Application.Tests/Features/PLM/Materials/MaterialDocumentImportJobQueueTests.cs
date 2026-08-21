using HRM.Application.Features.PLM.Materials.DocumentImport.Jobs;
using HRM.Domain.Enums.Attachment;
using HRM.Infrastructure.Services.FileStorage;

namespace HRM.Application.Tests.Features.PLM.Materials;

public sealed class MaterialDocumentImportJobQueueTests
{
    [Fact]
    public async Task Queue_AllowsOnlyOneActiveJobPerCompany()
    {
        var queue = new MaterialDocumentImportJobQueue();
        var companyId = Guid.NewGuid();

        var firstAccepted = queue.TryEnqueue(
            companyId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            out var first);
        var secondAccepted = queue.TryEnqueue(
            companyId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            out var active);

        Assert.True(firstAccepted);
        Assert.False(secondAccepted);
        Assert.Equal(first.JobId, active.JobId);

        var workItem = await queue.DequeueAsync(CancellationToken.None);
        Assert.Equal(first.JobId, workItem.JobId);
    }

    [Fact]
    public void Queue_TracksProgressAndReleasesCompanyAfterCompletion()
    {
        var queue = new MaterialDocumentImportJobQueue();
        var companyId = Guid.NewGuid();
        queue.TryEnqueue(companyId, Guid.NewGuid(), null, out var job);

        queue.MarkRunning(job.JobId, 4);
        queue.RecordItem(job.JobId, MaterialDocumentImportItemOutcome.Imported);
        queue.RecordItem(job.JobId, MaterialDocumentImportItemOutcome.Duplicate);
        queue.RecordItem(
            job.JobId,
            MaterialDocumentImportItemOutcome.RequiresReview,
            new MaterialDocumentImportJobException(
                "folder/file.pdf",
                "file.pdf",
                AttachmentSlot.MaterialOther,
                "material_code_not_detected",
                []));
        queue.RecordItem(job.JobId, MaterialDocumentImportItemOutcome.Failed);
        queue.MarkCompleted(job.JobId);

        var completed = Assert.IsType<MaterialDocumentImportJobSnapshot>(
            queue.Get(job.JobId, companyId));
        Assert.Equal(MaterialDocumentImportJobStatuses.Completed, completed.Status);
        Assert.Equal(4, completed.TotalFiles);
        Assert.Equal(4, completed.ProcessedFiles);
        Assert.Equal(1, completed.ImportedFiles);
        Assert.Equal(1, completed.DuplicateFiles);
        Assert.Equal(1, completed.RequiresReviewFiles);
        Assert.Equal(1, completed.FailedFiles);
        Assert.Single(queue.GetExceptions(job.JobId, companyId));

        Assert.True(queue.TryEnqueue(
            companyId,
            Guid.NewGuid(),
            null,
            out var next));
        Assert.NotEqual(job.JobId, next.JobId);
    }

    [Fact]
    public void Queue_DoesNotExposeJobAcrossCompanies()
    {
        var queue = new MaterialDocumentImportJobQueue();
        queue.TryEnqueue(Guid.NewGuid(), Guid.NewGuid(), null, out var job);

        Assert.Null(queue.Get(job.JobId, Guid.NewGuid()));
        Assert.Empty(queue.GetExceptions(job.JobId, Guid.NewGuid()));
    }
}
