using System.Security.Cryptography;
using System.Text;
using HRM.Domain.Entities.AttachmentSchema;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestGalleryImageContent;

internal static class GalleryThumbnailStorage
{
    public const int MaxEdge = 640;
    public const long MaxSourceBytes = 50_000_000;
    // Khóa theo dải có kích thước cố định; không giữ dictionary tăng theo số ảnh.
    private static readonly SemaphoreSlim[] Stripes = Enumerable.Range(0, 64)
        .Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    public static readonly SemaphoreSlim GenerationSlots = new(2, 2);

    public static SemaphoreSlim Gate(Guid id) => Stripes[(int)((uint)id.GetHashCode() % Stripes.Length)];

    public static string PathFor(AttachmentModel image)
    {
        var identity = $"{image.StoragePath}|{image.ContentHash}|{image.SizeBytes}|{image.CreateDate.Ticks}";
        var revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..24];
        var directory = Path.GetDirectoryName(image.StoragePath)?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Missing attachment directory.");
        return $"{directory}/gallery-thumbnails/v1-{MaxEdge}-{image.AttachmentId:N}-{revision}.webp";
    }
}
