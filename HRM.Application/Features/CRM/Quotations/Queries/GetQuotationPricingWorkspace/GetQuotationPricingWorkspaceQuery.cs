using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetQuotationPricingWorkspace;

public sealed record GetQuotationPricingWorkspaceQuery(Guid QuotationId)
    : IRequest<OperationResult<QuotationPricingWorkspaceDto>>;
