using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Queries.Tracking;

internal sealed class GetQuotationTrackingQueryHandler(QuotationTrackingReader reader)
    : IRequestHandler<GetQuotationTrackingQuery, QuotationTrackingDetailDto?>
{
    public async Task<QuotationTrackingDetailDto?> Handle(GetQuotationTrackingQuery request, CancellationToken cancellationToken)
    {
        var source = await reader.ReadAsync(cancellationToken);
        if (source is null) return null;

        var query = source.Where(x => x.QuotationId == request.QuotationId);
        var header = await QuotationTrackingReader.Project(query).SingleOrDefaultAsync(cancellationToken);
        if (header is null) return null;

        var lines = await query.SelectMany(x => x.Lines)
            .Where(line => line.IsActive)
            .OrderBy(line => line.QuotationLineId)
            .Select(line => new QuotationTrackingLineDto(line.ProductNameSnapshot, line.Quantity))
            .ToListAsync(cancellationToken);
        return new(header, lines);
    }
}
