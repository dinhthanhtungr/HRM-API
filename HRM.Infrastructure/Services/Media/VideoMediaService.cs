using System.Diagnostics;
using System.Text.Json;
using HRM.Application.Abstractions.FileStorage;
using HRM.Infrastructure.Services.FileStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HRM.Infrastructure.Services.Media;

/// <summary>Hàng đợi trên storage riêng; không public thư mục job hoặc bản phát.</summary>
internal sealed class VideoMediaService(
    IServiceScopeFactory scopes, IOptions<StorageOptions> storageOptions,
    IOptions<VideoMediaOptions> options, ILogger<VideoMediaService> logger)
    : BackgroundService, IVideoMediaService
{
    private readonly VideoMediaOptions _options = options.Value;
    private readonly string _root = string.IsNullOrWhiteSpace(storageOptions.Value.RootPath)
        ? string.Empty : Path.Combine(Path.GetFullPath(storageOptions.Value.RootPath), ".video-media");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private string Folder(Guid id) => Path.Combine(_root, id.ToString("N"));

    public async Task QueueAsync(Guid attachmentId, string storagePath, CancellationToken cancellationToken)
    {
        if (!_options.Enabled) return;
        if (string.IsNullOrEmpty(_root)) throw new InvalidOperationException("Storage:RootPath is required for video processing.");
        var folder = Folder(attachmentId);
        Directory.CreateDirectory(folder);
        var job = Path.Combine(folder, "job.json");
        if (File.Exists(job)) return;
        var temporary = Path.Combine(folder, $"job-{Guid.NewGuid():N}.tmp");
        // Chỉ công bố job hoàn chỉnh; request lặp không xếp cùng file hai lần.
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await JsonSerializer.SerializeAsync(stream, storagePath, Json, cancellationToken);
            File.Move(temporary, job, false);
        }
        catch (IOException) when (File.Exists(job)) { }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public async Task<VideoMediaInfo> GetInfoAsync(Guid attachmentId, CancellationToken cancellationToken)
    {
        if (!_options.Enabled) return new("Disabled");
        var path = Path.Combine(Folder(attachmentId), "state.json");
        if (!File.Exists(path)) return new("Processing");
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<VideoMediaInfo>(stream, Json, cancellationToken)
            ?? new VideoMediaInfo("Failed");
    }

    public async Task<StoredFile?> OpenAsync(Guid attachmentId, bool poster, CancellationToken cancellationToken)
    {
        if ((await GetInfoAsync(attachmentId, cancellationToken)).Status != "Ready") return null;
        var name = poster ? "poster.jpg" : "playback.mp4";
        var stream = File.OpenRead(Path.Combine(Folder(attachmentId), name));
        return new StoredFile(stream, poster ? "image/jpeg" : "video/mp4", name, stream.Length);
    }

    public async Task RetryAsync(Guid attachmentId, CancellationToken cancellationToken)
    {
        if ((await GetInfoAsync(attachmentId, cancellationToken)).Status != "Failed") return;
        var folder = Folder(attachmentId);
        await using var lease = new FileStream(Path.Combine(folder, "worker.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var counter = Path.Combine(folder, "retry.txt");
        var attempts = File.Exists(counter) ? int.Parse(await File.ReadAllTextAsync(counter, cancellationToken)) : 0;
        if (attempts >= 3) return;
        await File.WriteAllTextAsync(counter, (attempts + 1).ToString(), cancellationToken);
        File.Delete(Path.Combine(folder, "state.json"));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled) return;
        if (string.IsNullOrEmpty(_root)) throw new InvalidOperationException("Storage:RootPath is required for video processing.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Directory.CreateDirectory(_root);
                foreach (var job in Directory.EnumerateFiles(_root, "job.json", SearchOption.AllDirectories))
                {
                    var folder = Path.GetDirectoryName(job)!;
                    if (File.Exists(Path.Combine(folder, "state.json"))) continue;
                    FileStream lease;
                    try { lease = new FileStream(Path.Combine(folder, "worker.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
                    catch (IOException) { continue; }
                    await using (lease)
                    {
                        if (File.Exists(Path.Combine(folder, "state.json"))) continue;
                        try { await ProcessAsync(job, folder, stoppingToken); }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                        catch (Exception exception)
                        {
                            logger.LogWarning("Video processing failed ({ErrorType}).", exception.GetType().Name);
                            await WriteStateAsync(folder, new("Failed"), stoppingToken);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogWarning("Video queue unavailable ({ErrorType}).", exception.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private async Task ProcessAsync(string job, string folder, CancellationToken cancellationToken)
    {
        string? source;
        await using (var stream = File.OpenRead(job))
            source = await JsonSerializer.DeserializeAsync<string>(stream, Json, cancellationToken);
        if (string.IsNullOrWhiteSpace(source)) throw new InvalidDataException();
        using var scope = scopes.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var original = await storage.OpenReadAsync(source, cancellationToken);
        var input = Path.Combine(folder, "input" + Path.GetExtension(source));
        try
        {
            await using (original.Stream)
            await using (var target = File.Create(input))
                await original.Stream.CopyToAsync(target, cancellationToken);

            var probe = await RunAsync(_options.FfprobePath,
                ["-v", "error", "-protocol_whitelist", "file,pipe", "-format_whitelist", "mov,matroska,webm,avi,mpeg,mpegts", "-show_streams", "-show_format", "-of", "json", input],
                cancellationToken);
            using var document = JsonDocument.Parse(probe);
            var video = document.RootElement.GetProperty("streams").EnumerateArray()
                .First(x => x.TryGetProperty("codec_type", out var type) && type.GetString() == "video");
            var durationText = document.RootElement.GetProperty("format").GetProperty("duration").GetString();
            if (video.GetProperty("width").GetInt32() is <= 0 or > 8192 ||
                video.GetProperty("height").GetInt32() is <= 0 or > 8192)
                throw new InvalidDataException("Video dimensions are invalid.");
            if (!double.TryParse(durationText, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var duration)
                || !double.IsFinite(duration) || duration <= 0 || duration > _options.MaxDurationSeconds)
                throw new InvalidDataException("Video duration is invalid.");

            var output = Path.Combine(folder, "playback.tmp.mp4");
            await RunAsync(_options.FfmpegPath,
                ["-nostdin", "-v", "error", "-y", "-protocol_whitelist", "file,pipe", "-format_whitelist", "mov,matroska,webm,avi,mpeg,mpegts", "-i", input,
                    "-map", "0:v:0", "-map", "0:a:0?", "-vf", "scale=w='min(1280,iw)':h='min(1280,ih)':force_original_aspect_ratio=decrease:force_divisible_by=2",
                    "-c:v", "libx264", "-threads", "2", "-r", "30", "-preset", "veryfast", "-crf", "24", "-pix_fmt", "yuv420p",
                    "-c:a", "aac", "-b:a", "128k", "-movflags", "+faststart", output], cancellationToken);
            var poster = Path.Combine(folder, "poster.tmp.jpg");
            await RunAsync(_options.FfmpegPath,
                ["-nostdin", "-v", "error", "-y", "-protocol_whitelist", "file,pipe", "-i", output,
                    "-frames:v", "1", "-vf", "scale=480:-2", poster], cancellationToken);
            File.Move(output, Path.Combine(folder, "playback.mp4"), true);
            File.Move(poster, Path.Combine(folder, "poster.jpg"), true);
            await WriteStateAsync(folder, new("Ready", duration,
                video.GetProperty("width").GetInt32(), video.GetProperty("height").GetInt32()), cancellationToken);
            if (Guid.TryParseExact(Path.GetFileName(folder), "N", out var attachmentId))
            {
                try { await scope.ServiceProvider.GetRequiredService<IVideoMediaCompletionSignal>().PublishAsync(attachmentId, cancellationToken); }
                catch (Exception exception) when (exception is not OperationCanceledException)
                { logger.LogWarning("Media ready signal could not be queued ({ErrorType}); clients can poll.", exception.GetType().Name); }
            }
        }
        finally
        {
            foreach (var temporary in new[] { input, Path.Combine(folder, "playback.tmp.mp4"), Path.Combine(folder, "poster.tmp.jpg") })
                if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static async Task WriteStateAsync(string folder, VideoMediaInfo state, CancellationToken cancellationToken)
    {
        var temp = Path.Combine(folder, "state.tmp.json");
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(state, Json), cancellationToken);
        File.Move(temp, Path.Combine(folder, "state.json"), true);
    }

    private async Task<string> RunAsync(string executable, string[] arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start video processor.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 10, 1800)));
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            await error;
            if (process.ExitCode != 0) throw new InvalidDataException("Video processor rejected the file.");
            return await output;
        }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
    }
}
