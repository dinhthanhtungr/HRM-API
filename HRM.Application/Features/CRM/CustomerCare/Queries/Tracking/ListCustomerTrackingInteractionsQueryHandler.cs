using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Domain.Entities.CustomerSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.Tracking;

internal sealed class ListCustomerTrackingInteractionsQueryHandler(
    CustomerTrackingReader reader, ICRMReadDbContext db, ICurrentUser user)
    : IRequestHandler<ListCustomerTrackingInteractionsQuery, PagedResult<CustomerTrackingInteractionDto>?>
{
    private const int PageSize = 25;

    public async Task<PagedResult<CustomerTrackingInteractionDto>?> Handle(
        ListCustomerTrackingInteractionsQuery request, CancellationToken ct)
    {
        var customer = reader.Read()?.Where(row => row.CustomerId == request.CustomerId);
        if (customer is null || !await customer.AnyAsync(ct)) return null;

        var ids = customer.Select(row => row.CustomerId);
        var company = user.CompanyId.GetValueOrDefault();
        var query = db.CustomerInteractions.AsNoTracking().Where(row =>
            row.CompanyId == company && row.IsActive && ids.Contains(row.CustomerId));

        var count = await query.CountAsync(ct);
        var page = Math.Clamp(request.PageNumber, 1, Math.Max(1, (int)Math.Ceiling(count / (double)PageSize)));
        var rows = query.OrderByDescending(row => row.InteractionAt).ThenBy(row => row.Id)
            .Skip((page - 1) * PageSize).Take(PageSize);
        return new(await Project(rows).ToListAsync(ct), count, page, PageSize);
    }

    internal static IQueryable<CustomerTrackingInteractionDto> Project(IQueryable<CustomerInteraction> query) =>
        query.Select(row => new CustomerTrackingInteractionDto(
            row.Id, row.InteractionType.ToString(), row.InteractionAt, row.NextFollowUpDate));
}
