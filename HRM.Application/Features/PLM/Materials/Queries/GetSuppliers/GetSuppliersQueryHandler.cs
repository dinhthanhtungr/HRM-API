using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Pricing.Rules;
using HRM.Application.Commons.Rules;
using HRM.Application.Features.PLM.Materials.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Materials.Queries.GetSuppliers;

internal sealed class GetSuppliersQueryHandler : IRequestHandler<GetSuppliersQuery, PagedResult<SupplierSummaryDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetSuppliersQueryHandler(IPLMReadDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<SupplierSummaryDto>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        var empty = new PagedResult<SupplierSummaryDto>([], 0, request.NormalizedPageNumber, request.NormalizedPageSize);
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty) return empty;

        var query = _dbContext.Suppliers.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.IsActive == true);

        if (!string.IsNullOrWhiteSpace(request.NormalizedKeyword))
        {
            var pattern = PostgresSearchPattern.ContainsLiteral(request.NormalizedKeyword);
            query = query.Where(x =>
                EF.Functions.ILike(x.ExternalId ?? string.Empty, pattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.SupplierName ?? string.Empty, pattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Phone ?? string.Empty, pattern, PostgresSearchPattern.EscapeCharacter));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.SupplierName).ThenBy(x => x.ExternalId).ThenBy(x => x.SupplierId)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .Select(x => new SupplierSummaryDto
            {
                SupplierId = x.SupplierId,
                SupplierCode = x.ExternalId ?? string.Empty,
                SupplierName = x.SupplierName ?? string.Empty,
                Phone = x.Phone,
                ContactName = x.SupplierContacts.Where(c => c.IsActive == true)
                    .OrderByDescending(c => c.IsPrimary == true).ThenBy(c => c.ContactId)
                    .Select(c => ((c.FirstName ?? string.Empty) + " " + (c.LastName ?? string.Empty)).Trim())
                    .FirstOrDefault(),
                ContactPhone = x.SupplierContacts.Where(c => c.IsActive == true)
                    .OrderByDescending(c => c.IsPrimary == true).ThenBy(c => c.ContactId)
                    .Select(c => c.Phone).FirstOrDefault(),
                LatestOrderDate = x.PurchaseOrders
                    .Where(o => o.CompanyId == companyId && o.IsActive == true &&
                                (o.Status == null || !PurchaseOrderPriceRules.CanceledStatuses.Contains(o.Status)))
                    .Max(o => o.CreateDate)
            }).ToListAsync(cancellationToken);

        return new PagedResult<SupplierSummaryDto>(items, total, request.NormalizedPageNumber, request.NormalizedPageSize);
    }
}
