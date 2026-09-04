using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetQuotationCustomerTerms;

public sealed record GetQuotationCustomerTermsQuery(Guid CustomerId)
    : IRequest<OperationResult<QuotationCustomerTermsDto>>;
