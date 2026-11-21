using System.Text.Json;
using HRM.Application.Abstractions.FileStorage;
using HRM.Infrastructure.Services.FileStorage;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HRM.Infrastructure.Services.Media;

internal sealed class ResumableAttachmentStore(IOptions<StorageOptions> options,
    ILogger<ResumableAttachmentStore>? logger = null) : BackgroundService, IResumableAttachmentStore
{
    public const int ChunkBytes = 1024 * 1024;
    private readonly string _root = string.IsNullOrWhiteSpace(options.Value.RootPath)
        ? string.Empty : Path.Combine(Path.GetFullPath(options.Value.RootPath), ".attachment-uploads");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private string Folder(AttachmentUploadOwner owner, Guid id)
        => Path.Combine(_root, owner.CompanyId.ToString("N"), owner.EmployeeId.ToString("N"), id.ToString("N"));

    public async Task CreateAsync(AttachmentUploadOwner owner, AttachmentUploadSession session, CancellationToken cancellationToken)
    {
        var folder = Folder(owner, session.SessionId);
        if (string.IsNullOrEmpty(_root)) throw new InvalidOperationException("Storage:RootPath is required for uploads.");
        Directory.CreateDirectory(folder);
        await using var lease = await LockAsync(owner, session.SessionId, cancellationToken);
        await using var ownerLease = await AcquireFileLockAsync(Path.Combine(Directory.GetParent(folder)!.FullName, "owner.lock"), cancellationToken);
        var path = Path.Combine(folder, "session.json");
        if (File.Exists(path))
        {
            var existing = await GetAsync(owner, session.SessionId, cancellationToken);
            if (existing.ConversationId != session.ConversationId || existing.AreaCode != session.AreaCode
                || !existing.Files.SequenceEqual(session.Files)) throw new InvalidOperationException("Upload session does not match.");
            return;
        }
        // Giới hạn dung lượng tạm theo người gửi; chỉ dọn các session đã hết hạn.
        var parent = Directory.GetParent(folder)!.FullName;
        var active = 0;
        foreach (var directory in Directory.EnumerateDirectories(parent))
        {
            var metadata = Path.Combine(directory, "session.json");
            if (!File.Exists(metadata)) continue;
            var current = JsonSerializer.Deserialize<AttachmentUploadSession>(await File.ReadAllTextAsync(metadata, cancellationToken), Json);
            if (current is null || current.ExpiresAt > DateTimeOffset.UtcNow) { active++; continue; }
            try
            {
                using (new FileStream(Path.Combine(directory, "session.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                    foreach (var file in Directory.EnumerateFiles(directory).Where(f => Path.GetFileName(f) != "session.lock")) File.Delete(file);
            }
            catch (IOException) { }
        }
        if (active >= 3) throw new InvalidOperationException("Too many pending uploads. Cancel an existing upload first.");
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(session, Json), cancellationToken);
        File.Move(temporary, path, true);
    }

    public async Task<AttachmentUploadSession> GetAsync(AttachmentUploadOwner owner, Guid id, CancellationToken cancellationToken)
    {
        var path = Path.Combine(Folder(owner, id), "session.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Upload session was not found.");
        var session = JsonSerializer.Deserialize<AttachmentUploadSession>(await File.ReadAllTextAsync(path, cancellationToken), Json);
        if (session is null || session.ExpiresAt < DateTimeOffset.UtcNow) throw new FileNotFoundException("Upload session expired.");
        return session;
    }

    public async Task<AttachmentUploadProgress> GetProgressAsync(AttachmentUploadOwner owner, Guid id, CancellationToken cancellationToken)
    {
        var session = await GetAsync(owner, id, cancellationToken);
        return new(id, Enumerable.Range(0, session.Files.Count).Select(i =>
        {
            var path = Path.Combine(Folder(owner, id), $"{i}.part");
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }).ToArray());
    }

    public async Task<long> AppendAsync(AttachmentUploadOwner owner, Guid id, int index, long offset, Stream chunk, CancellationToken cancellationToken)
    {
        await using var lease = await LockAsync(owner, id, cancellationToken);
        var session = await GetAsync(owner, id, cancellationToken);
        if (index < 0 || index >= session.Files.Count || offset < 0) throw new InvalidOperationException("Invalid file index or offset.");
        await using var file = new FileStream(Path.Combine(Folder(owner, id), $"{index}.part"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 81920, true);
        if (file.Length != offset) throw new InvalidOperationException("Upload offset changed. Read progress before retrying.");
        file.Position = offset;
        var buffer = new byte[81920];
        var received = 0;
        try
        {
            int count;
            while ((count = await chunk.ReadAsync(buffer, cancellationToken)) > 0)
            {
                received += count;
                if (received > ChunkBytes || offset + received > session.Files[index].Length)
                    throw new InvalidOperationException("Upload chunk exceeds the allowed size.");
                await file.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            }
            if (received == 0) throw new InvalidOperationException("Upload chunk is empty.");
            return file.Length;
        }
        catch { file.SetLength(offset); throw; }
    }

    public async Task<Stream> OpenAsync(AttachmentUploadOwner owner, Guid id, int index, CancellationToken cancellationToken)
    {
        var session = await GetAsync(owner, id, cancellationToken);
        if (index < 0 || index >= session.Files.Count) throw new InvalidOperationException("Invalid file index.");
        var stream = File.OpenRead(Path.Combine(Folder(owner, id), $"{index}.part"));
        if (stream.Length == session.Files[index].Length) return stream;
        await stream.DisposeAsync();
        throw new InvalidOperationException("Upload is incomplete.");
    }

    public async Task<IAsyncDisposable> LockAsync(AttachmentUploadOwner owner, Guid id, CancellationToken cancellationToken)
    {
        var folder = Folder(owner, id);
        if (!Directory.Exists(folder)) throw new FileNotFoundException("Upload session was not found.");
        return await AcquireFileLockAsync(Path.Combine(folder, "session.lock"), cancellationToken);
    }

    private static async Task<FileStream> AcquireFileLockAsync(string path, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (attempt < 100) { await Task.Delay(100, cancellationToken); }
        }
    }

    public async Task CancelAsync(AttachmentUploadOwner owner, Guid id, CancellationToken cancellationToken)
    {
        await using var lease = await LockAsync(owner, id, cancellationToken);
        await GetAsync(owner, id, cancellationToken);
        foreach (var file in Directory.EnumerateFiles(Folder(owner, id)).Where(f => Path.GetFileName(f) != "session.lock")) File.Delete(file);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrEmpty(_root)) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (Directory.Exists(_root))
                    foreach (var path in Directory.EnumerateFiles(_root, "session.json", SearchOption.AllDirectories))
                    {
                        try
                        {
                            await using var lease = new FileStream(Path.Combine(Path.GetDirectoryName(path)!, "session.lock"),
                                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                            var session = JsonSerializer.Deserialize<AttachmentUploadSession>(await File.ReadAllTextAsync(path, stoppingToken), Json);
                            if (session is null || session.ExpiresAt > DateTimeOffset.UtcNow) continue;
                            foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(path)!).Where(f => Path.GetFileName(f) != "session.lock"))
                                File.Delete(file);
                        }
                        catch (IOException) { }
                    }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger?.LogWarning("Upload cleanup failed ({ErrorType}).", exception.GetType().Name); }
            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }
}
