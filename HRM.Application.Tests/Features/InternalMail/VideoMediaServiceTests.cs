using HRM.Application.Abstractions.FileStorage;
using HRM.Infrastructure.Services.FileStorage;
using HRM.Infrastructure.Services.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed class VideoMediaServiceTests
{
    [Fact]
    public async Task MissingProcessorBecomesFailedInsteadOfLeavingVideoProcessingForever()
    {
        var root = Path.Combine(Path.GetTempPath(), "hrm-video-worker-" + Guid.NewGuid().ToString("N"));
        var storageOptions = Options.Create(new StorageOptions { RootPath = root });
        var storage = new FileShareStorage(storageOptions);
        using var services = new ServiceCollection().AddSingleton<IFileStorage>(storage).BuildServiceProvider();
        using var worker = new VideoMediaService(services.GetRequiredService<IServiceScopeFactory>(), storageOptions,
            Options.Create(new VideoMediaOptions { Enabled = true, FfprobePath = Path.Combine(root, "missing-ffprobe.exe") }),
            NullLogger<VideoMediaService>.Instance);
        var id = Guid.NewGuid();
        try
        {
            await storage.SaveAtPathAsync(new MemoryStream([1, 2, 3]), "source.mp4");
            await Task.WhenAll(worker.QueueAsync(id, "source.mp4", default), worker.QueueAsync(id, "source.mp4", default));
            await worker.StartAsync(default);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            VideoMediaInfo state;
            do
            {
                await Task.Delay(30, timeout.Token);
                state = await worker.GetInfoAsync(id, timeout.Token);
            } while (state.Status == "Processing");
            Assert.Equal("Failed", state.Status);
            Assert.Null(await worker.OpenAsync(id, false, default));
            Assert.False(File.Exists(Path.Combine(root, ".video-media", id.ToString("N"), "input.mp4")));
        }
        finally
        {
            await worker.StopAsync(default);
            var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (Path.GetFullPath(root).StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(root).StartsWith("hrm-video-worker-", StringComparison.Ordinal) && Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task DisabledProcessorDoesNotStartJobsOrRequireAStorageRoot()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        using var worker = new VideoMediaService(services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new StorageOptions()), Options.Create(new VideoMediaOptions()), NullLogger<VideoMediaService>.Instance);
        await worker.StartAsync(default);
        await worker.QueueAsync(Guid.NewGuid(), "not-read.mp4", default);
        Assert.Equal("Disabled", (await worker.GetInfoAsync(Guid.NewGuid(), default)).Status);
        await worker.StopAsync(default);
    }
}
