using HRM.Application.Commons.Models;
using HRM.Application.Features.MessageRecipients.Dtos;
using HRM.Application.Features.MessageRecipients.Services;
using MediatR;

namespace HRM.Application.Features.MessageRecipients.Queries.PreviewMessageRecipients;

internal sealed class PreviewMessageRecipientsQueryHandler
    : IRequestHandler<PreviewMessageRecipientsQuery, OperationResult<MessageRecipientPreviewDto>>
{
    private readonly IEnumerable<IMessageRecipientResolver> _resolvers;

    public PreviewMessageRecipientsQueryHandler(IEnumerable<IMessageRecipientResolver> resolvers)
    {
        _resolvers = resolvers;
    }

    public Task<OperationResult<MessageRecipientPreviewDto>> Handle(
        PreviewMessageRecipientsQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ContextType))
        {
            return Task.FromResult(OperationResult<MessageRecipientPreviewDto>.Fail("ContextType is required."));
        }

        if (string.IsNullOrWhiteSpace(request.ActionType))
        {
            return Task.FromResult(OperationResult<MessageRecipientPreviewDto>.Fail("ActionType is required."));
        }

        var resolver = _resolvers.FirstOrDefault(x => x.CanResolve(request.ContextType.Trim()));
        return resolver is null
            ? Task.FromResult(OperationResult<MessageRecipientPreviewDto>.Fail("Recipient preview context is not supported."))
            : resolver.PreviewAsync(request, cancellationToken);
    }
}
