using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.Tracking;

internal sealed class GetCustomerTrackingQueryHandler(CustomerTrackingReader reader)
    : IRequestHandler<GetCustomerTrackingQuery, CustomerTrackingDto?>
{
    public Task<CustomerTrackingDto?> Handle(GetCustomerTrackingQuery request, CancellationToken ct)
    {
        var query = reader.Read();
        return query is null
            ? Task.FromResult<CustomerTrackingDto?>(null)
            : reader.Project(query.Where(customer => customer.CustomerId == request.CustomerId)).SingleOrDefaultAsync(ct);
    }
}
