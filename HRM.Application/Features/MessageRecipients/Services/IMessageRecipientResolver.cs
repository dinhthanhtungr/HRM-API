using HRM.Application.Commons.Models;
using HRM.Application.Features.MessageRecipients.Dtos;
using HRM.Application.Features.MessageRecipients.Queries.PreviewMessageRecipients;

namespace HRM.Application.Features.MessageRecipients.Services;

public interface IMessageRecipientResolver
{
    bool CanResolve(string contextType);

    Task<OperationResult<MessageRecipientPreviewDto>> PreviewAsync(
        PreviewMessageRecipientsQuery request,
        CancellationToken cancellationToken);
}
