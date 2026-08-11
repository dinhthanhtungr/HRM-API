using HRM.Application.Abstractions.FileStorage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace HRM.Infrastructure.Services.FileStorage;

internal sealed class ImageSharpThumbnailGenerator : IImageThumbnailGenerator
{
    private const long MaxPixelCount = 40_000_000;

    public async Task<Stream?> GenerateWebpAsync(
        Stream source,
        int maxWidth,
        int maxHeight,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var image = await Image.LoadAsync(source, cancellationToken);
            if ((long)image.Width * image.Height > MaxPixelCount)
            {
                return null;
            }

            image.Mutate(context => context
                .AutoOrient()
                .Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(maxWidth, maxHeight)
                }));

            var output = new MemoryStream();
            await image.SaveAsWebpAsync(
                output,
                new WebpEncoder { Quality = 78 },
                cancellationToken);
            output.Position = 0;
            return output;
        }
        catch (UnknownImageFormatException)
        {
            return null;
        }
        catch (InvalidImageContentException)
        {
            return null;
        }
    }
}
