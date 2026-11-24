using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Searching;
using HRM.Application.Features.PLM.SaleOrders.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.Queries;

public sealed record ComplaintSourceOrder(Guid Id, string Code, DateTime Date);
public sealed record ComplaintSourceLine(Guid Id, Guid ProductId, string ProductName, List<ComplaintOrderVaOption> Va);
public sealed record ComplaintOrderSources(List<ComplaintSourceOrder> Orders, List<ComplaintSourceLine> Lines);
public sealed record GetComplaintOrderSourcesQuery(Guid CustomerId, Guid? OrderId, string? Keyword)
    : IRequest<ComplaintOrderSources>;

internal sealed class GetComplaintOrderSourcesQueryHandler(ISaleOrderDbContext db, ICurrentUser user)
    : IRequestHandler<GetComplaintOrderSourcesQuery, ComplaintOrderSources>
{
    public async Task<ComplaintOrderSources> Handle(GetComplaintOrderSourcesQuery request, CancellationToken ct)
    {
        var companyId = SaleOrderCurrentUserGuard.RequireCompanyId(user);
        var orders = db.MerchandiseOrders.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.CustomerId == request.CustomerId && x.IsActive);
        if (request.OrderId is not { } orderId)
        {
            if (!string.IsNullOrWhiteSpace(request.Keyword))
            {
                var pattern = PostgresSearchPattern.ContainsLiteral(request.Keyword.Trim());
                orders = orders.Where(x => EF.Functions.ILike(x.ExternalId, pattern, PostgresSearchPattern.EscapeCharacter));
            }
            return new(await orders.OrderByDescending(x => x.CreateDate).ThenBy(x => x.MerchandiseOrderId)
                .Take(30).Select(x => new ComplaintSourceOrder(x.MerchandiseOrderId, x.ExternalId, x.CreateDate))
                .ToListAsync(ct), []);
        }

        var lines = await orders.Where(x => x.MerchandiseOrderId == orderId)
            .SelectMany(x => x.MerchandiseOrderDetails).Where(x => x.IsActive)
            .Select(x => new { x.MerchandiseOrderDetailId, x.ProductId, x.ProductNameSnapshot })
            .ToListAsync(ct);
        var result = new List<ComplaintSourceLine>(lines.Count);
        foreach (var line in lines)
        {
            result.Add(new(line.MerchandiseOrderDetailId, line.ProductId, line.ProductNameSnapshot,
                await ComplaintOrderSourceReader.ReadVaAsync(db, companyId, line.MerchandiseOrderDetailId, ct)));
        }
        return new([], result);
    }
}
