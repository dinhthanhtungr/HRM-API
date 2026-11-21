using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.Tracking;

public sealed record GetCustomerTrackingQuery(Guid CustomerId) : IRequest<CustomerTrackingDto?>;
