using HRM.Application.Features.InternalMail.Dtos;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed class InternalMailChatAttachmentMatrixTests
{
    public static IEnumerable<object[]> AttachmentCases()
    {
        var formats = new (string Extension, string Mime, string Kind)[] {
            ("png", "image/png", "Image"), ("jpg", "image/jpeg", "Image"), ("jpeg", "image/jpeg", "Image"),
            ("gif", "image/gif", "Image"), ("webp", "image/webp", "Image"), ("bmp", "image/bmp", "Image"),
            ("pdf", "application/pdf", "Pdf"), ("doc", "application/msword", "Document"),
            ("docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "Document"),
            ("xls", "application/vnd.ms-excel", "Spreadsheet"),
            ("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Spreadsheet"),
            ("txt", "text/plain", "Text"), ("csv", "text/csv", "Spreadsheet"), ("zip", "application/zip", "Archive"),
            ("unknown", "application/octet-stream", "File") };
        foreach (var format in formats)
        foreach (var source in new[] { "Chat", "SampleRequest" })
        foreach (var upper in new[] { false, true })
            yield return [format.Extension, format.Mime, format.Kind, source, upper];
    }

    [Theory]
    [MemberData(nameof(AttachmentCases))]
    public void ChatAttachmentMatrix_UsesProtectedRoutesAndImageOnlyThumbnails(
        string extension, string mime, string kind, string source, bool upper)
    {
        var id = Guid.NewGuid();
        var conversation = Guid.NewGuid();
        var dto = new InternalConversationAttachmentDto { AttachmentId = id, Source = source,
            FileName = $"Ảnh mẫu kiểm thử.{(upper ? extension.ToUpperInvariant() : extension)}" };
        InternalMessageAttachmentPresentation.Enrich(dto, conversation);
        var route = source == "SampleRequest"
            ? $"/api/v1/internal-mail/conversations/{conversation}/related-attachments/{id}"
            : $"/api/v1/internal-mail/attachments/{id}";
        Assert.Equal(route, dto.ContentUrl);
        Assert.Equal(route + "?mode=download", dto.DownloadUrl);
        Assert.Equal(mime, dto.ContentType);
        Assert.Equal(kind, dto.Kind);
        Assert.Equal(kind == "Image", dto.IsImage);
        Assert.Equal(kind == "Image" ? route + "/thumbnail" : null, dto.ThumbnailUrl);
        Assert.DoesNotContain(dto.FileName, dto.ContentUrl);
    }

    [Theory]
    [InlineData("sample.mp4", "video/mp4")]
    [InlineData("sample.MOV", "video/quicktime")]
    [InlineData("sample.webm", "video/webm")]
    public void VideoHasItsOwnMediaKindAndProtectedPlaybackRoutes(string name, string mime)
    {
        var attachment = new InternalMessageAttachmentDto { AttachmentId = Guid.NewGuid(), FileName = name };
        InternalMessageAttachmentPresentation.Enrich(attachment);
        Assert.Equal(HRM.Domain.Enums.Attachment.AttachmentMediaKind.Video, attachment.MediaKind);
        Assert.False(attachment.IsImage);
        Assert.Equal(mime, attachment.ContentType);
        Assert.Equal(attachment.ContentUrl + "/video/info", attachment.VideoInfoUrl);
        Assert.Equal(attachment.ContentUrl + "/video/playback", attachment.PlaybackUrl);
        Assert.Equal(attachment.ContentUrl + "/video/poster", attachment.PosterUrl);
    }
}
