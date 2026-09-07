using HRM.Application.Features.PLM.CustomerLabels.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.CustomerLabels.Queries.GetCustomerLabelById;

public sealed record GetCustomerLabelByIdQuery(Guid CustomerLabelHeaderId) : IRequest<CustomerLabelDto?>;
