namespace HRM.Application.Abstractions.FileStorage;

public interface IVideoMediaCompletionSignal
{
    Task PublishAsync(Guid attachmentId, CancellationToken cancellationToken);
}
