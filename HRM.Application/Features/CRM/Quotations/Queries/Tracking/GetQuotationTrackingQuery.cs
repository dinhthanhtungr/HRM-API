using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Queries.Tracking;

public sealed record GetQuotationTrackingQuery(Guid QuotationId) : IRequest<QuotationTrackingDetailDto?>;
