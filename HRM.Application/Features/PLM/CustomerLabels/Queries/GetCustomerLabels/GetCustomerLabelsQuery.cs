using HRM.Application.Features.PLM.CustomerLabels.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.CustomerLabels.Queries.GetCustomerLabels;

public sealed class GetCustomerLabelsQuery : IRequest<IReadOnlyList<CustomerLabelDto>>
{
    public Guid? ProductId { get; init; }
    public Guid? CustomerId { get; init; }
    public bool? IsActive { get; init; }
}
