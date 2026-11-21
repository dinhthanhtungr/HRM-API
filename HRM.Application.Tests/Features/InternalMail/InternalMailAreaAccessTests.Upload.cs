using HRM.Application.Abstractions.FileStorage;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Fact]
    public async Task UploadCommitReplayReturnsStoredMessageWithoutReadingPartsOrPublishingAgain()
    {
        var user = new User("SaleUser");
        var current = CreateStore(user, InternalMailRelatedType.Internal, true);
        var message = current.Db.InternalMessages.Single();
        message.Conversation = current.Child;
        message.SenderEmployeeId = user.EmployeeId!.Value;
        var root = Path.Combine(Path.GetTempPath(), "hrm-video-replay-" + Guid.NewGuid().ToString("N"));
        using var storage = new HRM.Infrastructure.Services.Media.ResumableAttachmentStore(
            Microsoft.Extensions.Options.Options.Create(new HRM.Infrastructure.Services.FileStorage.StorageOptions { RootPath = root }));
        try
        {
            var service = new InternalMailUploadService(storage, user,
                new InternalMailAreaAccessService(current.Db, user), current.Db, null!);
            var progress = await service.StartAsync(new(message.InternalMessageId, current.Child.InternalConversationId,
                "general", [new("sample.mp4", "video/mp4", 10)]), default);
            Assert.Equal(0, progress.Offsets[0]);
            var result = await service.CompleteAsync(message.InternalMessageId, new() { Body = "retry" }, default);
            Assert.True(result.Success);
            Assert.Equal(message.InternalMessageId, result.Data!.MessageId);
            Assert.Equal(Guid.Empty, result.Data.NotificationId);
            // Không tạo .part; nếu replay đọc file hoặc gọi sender null thì test phải thất bại.
        }
        finally
        {
            var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (Path.GetFullPath(root).StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(root).StartsWith("hrm-video-replay-", StringComparison.Ordinal) && Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task UploadCannotTouchStorageWithoutAuthenticationMembershipAndCompany(bool authenticated, bool member, bool sameCompany)
    {
        var user = new User("SaleUser") { IsAuthenticated = authenticated };
        var current = CreateStore(user, InternalMailRelatedType.ConversationCustom, member);
        if (!sameCompany) user.CompanyId = Guid.NewGuid();
        var areas = new InternalMailAreaAccessService(current.Db, user);
        // Null storage/sender chủ ý: test phải bị từ chối trước mọi side effect.
        var uploads = new InternalMailUploadService(null!, user, areas, current.Db, null!);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => uploads.StartAsync(new(Guid.NewGuid(),
            current.Child.InternalConversationId, "custom", [new AttachmentUploadDescriptor("a.mp4", "video/mp4", 20)]), default));
    }

    [Theory]
    [InlineData("file.exe", "text/plain", 10)]
    [InlineData("../video.mp4", "video/mp4", 10)]
    [InlineData("video.mp4", "application/pdf", 10)]
    [InlineData("file.pdf", "video/mp4", 10)]
    [InlineData("video.mp4", "video/mp4", 60_000_000)]
    public async Task UploadRejectsUnsafeNamesTypesAndSizesBeforeWriting(string name, string type, long length)
    {
        var user = new User("SaleUser");
        var current = CreateStore(user, InternalMailRelatedType.Internal, true);
        var areas = new InternalMailAreaAccessService(current.Db, user);
        var uploads = new InternalMailUploadService(null!, user, areas, current.Db, null!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => uploads.StartAsync(new(Guid.NewGuid(),
            current.Child.InternalConversationId, "general", [new AttachmentUploadDescriptor(name, type, length)]), default));
    }
}
