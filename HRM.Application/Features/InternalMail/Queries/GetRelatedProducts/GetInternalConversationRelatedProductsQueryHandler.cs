using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Queries.GetRelatedProducts;

internal sealed class GetInternalConversationRelatedProductsQueryHandler(
    IInternalMailDbContext db, ICurrentUser user, InternalMailAreaAccessService areas)
    : IRequestHandler<GetInternalConversationRelatedProductsQuery, IReadOnlyList<InternalConversationRelatedProductDto>?>
{
    public async Task<IReadOnlyList<InternalConversationRelatedProductDto>?> Handle(
        GetInternalConversationRelatedProductsQuery request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is not { } company || user.EmployeeId is null ||
            request.ConversationId == Guid.Empty) return null;

        var source = await areas.Conversations().AsNoTracking()
            .Where(c => c.InternalConversationId == request.ConversationId && c.RelatedType == InternalMailRelatedType.Quotation &&
                db.Quotations.Any(q => q.QuotationId == c.RelatedId && q.CompanyId == company && q.IsActive))
            .Select(c => c.RelatedId).FirstOrDefaultAsync(ct);
        if (source is null) return null;

        var lines = db.Quotations.AsNoTracking()
            .Where(q => q.QuotationId == source && q.CompanyId == company && q.IsActive)
            .SelectMany(q => q.Lines)
            .Where(l => l.IsActive && l.ProductNavigation.CompanyId == company);
        var products = await lines.OrderBy(l => l.SortOrder).ThenBy(l => l.QuotationLineId)
            .Select(l => new { l.ProductId, l.ProductExternalIdSnapshot, l.ProductNameSnapshot }).ToListAsync(ct);
        if (products.Count == 0) return [];

        // Ưu tiên liên kết đã lưu. Dòng cũ chưa có SampleRequestId liệt kê mọi yêu cầu mẫu đúng ProductId,
        // không đoán theo mã/tên hoặc tự thay liên kết đã bị thu hồi bằng yêu cầu mẫu khác.
        var targets = await (from line in lines
            join sample in db.SampleRequests.AsNoTracking().Where(s => s.CompanyId == company && s.IsActive)
                on line.ProductId equals sample.ProductId
            join conversation in areas.Conversations().AsNoTracking()
                    .Where(c => c.RelatedType == InternalMailRelatedType.SampleRequest)
                on sample.SampleRequestId equals conversation.RelatedId
            where !line.SampleRequestId.HasValue || line.SampleRequestId == sample.SampleRequestId
            select new
            {
                line.ProductId, SampleRequestId = sample.SampleRequestId, RequestCode = sample.ExternalId,
                ConversationId = conversation.InternalConversationId
            }).Distinct().ToListAsync(ct);

        return products.DistinctBy(p => p.ProductId).Select(product => new InternalConversationRelatedProductDto(
            product.ProductId, product.ProductExternalIdSnapshot, product.ProductNameSnapshot,
            targets.Where(t => t.ProductId == product.ProductId).OrderBy(t => t.RequestCode).ThenBy(t => t.ConversationId)
                .Select(t => new InternalConversationRelatedProductTargetDto(t.ConversationId, t.SampleRequestId, t.RequestCode))
                .ToArray())).ToArray();
    }
}
