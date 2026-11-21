using HRM.Application.Abstractions.FileStorage;
using HRM.Infrastructure.Services.FileStorage;
using HRM.Infrastructure.Services.Media;
using Microsoft.Extensions.Options;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed class ResumableAttachmentStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "hrm-video-tests-" + Guid.NewGuid().ToString("N"));
    private readonly AttachmentUploadOwner _owner = new(Guid.NewGuid(), Guid.NewGuid());
    private readonly Guid _id = Guid.NewGuid();
    private ResumableAttachmentStore Store => new(Options.Create(new StorageOptions { RootPath = _root }));

    private Task StartAsync(long size = 8) => Store.CreateAsync(_owner,
        new(_id, Guid.NewGuid(), "general", [new("sample.mp4", "video/mp4", size)], DateTimeOffset.UtcNow.AddHours(1)), default);

    [Fact]
    public async Task ResumeAfterStoreRestartPreservesBytesAndOffset()
    {
        await StartAsync();
        Assert.Equal(4, await Store.AppendAsync(_owner, _id, 0, 0, new MemoryStream([1, 2, 3, 4]), default));
        Assert.Equal(4, (await Store.GetProgressAsync(_owner, _id, default)).Offsets[0]);
        Assert.Equal(8, await Store.AppendAsync(_owner, _id, 0, 4, new MemoryStream([5, 6, 7, 8]), default));
        await using var stream = await Store.OpenAsync(_owner, _id, 0, default);
        var output = new MemoryStream();
        await stream.CopyToAsync(output);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, output.ToArray());
    }

    [Fact]
    public async Task AnotherEmployeeOrCompanyCannotReadUpload()
    {
        await StartAsync();
        await Assert.ThrowsAsync<FileNotFoundException>(() => Store.GetAsync(_owner with { EmployeeId = Guid.NewGuid() }, _id, default));
        await Assert.ThrowsAsync<FileNotFoundException>(() => Store.GetAsync(_owner with { CompanyId = Guid.NewGuid() }, _id, default));
    }

    [Fact]
    public async Task WrongOffsetAndIncompleteUploadCannotCommit()
    {
        await StartAsync();
        await Store.AppendAsync(_owner, _id, 0, 0, new MemoryStream([1, 2]), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store.AppendAsync(_owner, _id, 0, 0, new MemoryStream([3]), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store.OpenAsync(_owner, _id, 0, default));
        Assert.Equal(2, (await Store.GetProgressAsync(_owner, _id, default)).Offsets[0]);
    }

    [Fact]
    public async Task OversizedChunkRollsBackPartialWrite()
    {
        await StartAsync(2 * ResumableAttachmentStore.ChunkBytes);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store.AppendAsync(_owner, _id, 0, 0,
            new MemoryStream(new byte[ResumableAttachmentStore.ChunkBytes + 1]), default));
        Assert.Equal(0, (await Store.GetProgressAsync(_owner, _id, default)).Offsets[0]);
    }

    [Fact]
    public async Task ExpiredAndCancelledSessionsAreNotReadable()
    {
        await Store.CreateAsync(_owner, new(_id, Guid.NewGuid(), null,
            [new("a.pdf", "application/pdf", 8)], DateTimeOffset.UtcNow.AddSeconds(-1)), default);
        await Assert.ThrowsAsync<FileNotFoundException>(() => Store.GetAsync(_owner, _id, default));
        var second = Guid.NewGuid();
        await Store.CreateAsync(_owner, new(second, Guid.NewGuid(), null,
            [new("a.pdf", "application/pdf", 8)], DateTimeOffset.UtcNow.AddHours(1)), default);
        await Store.CancelAsync(_owner, second, default);
        await Assert.ThrowsAsync<FileNotFoundException>(() => Store.GetAsync(_owner, second, default));
    }

    [Fact]
    public async Task SessionIdCannotBeReusedForDifferentConversationOrFiles()
    {
        await StartAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store.CreateAsync(_owner,
            new(_id, Guid.NewGuid(), "general", [new("sample.mp4", "video/mp4", 8)], DateTimeOffset.UtcNow.AddHours(1)), default));
    }

    public void Dispose()
    {
        var expected = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (Path.GetFullPath(_root).StartsWith(expected, StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(_root).StartsWith("hrm-video-tests-", StringComparison.Ordinal) && Directory.Exists(_root))
            Directory.Delete(_root, true);
    }
}
