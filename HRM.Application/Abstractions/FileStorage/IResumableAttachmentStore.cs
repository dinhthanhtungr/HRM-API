namespace HRM.Application.Abstractions.FileStorage;

public sealed record AttachmentUploadOwner(Guid CompanyId, Guid EmployeeId);
public sealed record AttachmentUploadDescriptor(string FileName, string ContentType, long Length);
public sealed record AttachmentUploadSession(Guid SessionId, Guid ConversationId, string? AreaCode,
    IReadOnlyList<AttachmentUploadDescriptor> Files, DateTimeOffset ExpiresAt);
public sealed record AttachmentUploadProgress(Guid SessionId, IReadOnlyList<long> Offsets);

public interface IResumableAttachmentStore
{
    Task CreateAsync(AttachmentUploadOwner owner, AttachmentUploadSession session, CancellationToken cancellationToken);
    Task<AttachmentUploadSession> GetAsync(AttachmentUploadOwner owner, Guid id, CancellationToken cancellationToken);
    Task<AttachmentUploadProgress> GetProgressAsync(AttachmentUploadOwner owner, Guid id, CancellationToken cancellationToken);
    Task<long> AppendAsync(AttachmentUploadOwner owner, Guid id, int index, long offset, Stream chunk, CancellationToken cancellationToken);
    Task<Stream> OpenAsync(AttachmentUploadOwner owner, Guid id, int index, CancellationToken cancellationToken);
    Task<IAsyncDisposable> LockAsync(AttachmentUploadOwner owner, Guid id, CancellationToken cancellationToken);
    Task CancelAsync(AttachmentUploadOwner owner, Guid id, CancellationToken cancellationToken);
}
