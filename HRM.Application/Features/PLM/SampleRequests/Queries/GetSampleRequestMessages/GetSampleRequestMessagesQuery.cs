using HRM.Application.Features.PLM.SampleRequests.Dtos.InternalMail;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestMessages;

/// <summary>
/// Lay lich su notification/trao doi cua mot SampleRequest de FE render nhu thread email nho.
/// </summary>
public sealed class GetSampleRequestMessagesQuery
    : IRequest<IReadOnlyList<SampleRequestMessageDto>?>
{
    public Guid SampleRequestId { get; init; }
}
