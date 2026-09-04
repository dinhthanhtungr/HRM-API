using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Commands.UpdateQuotationCustomerPriceTiers;

public sealed record UpdateQuotationCustomerPriceTiersCommand(
    Guid QuotationId,
    UpdateQuotationCustomerPriceTiersRequest Request)
    : IRequest<OperationResult<QuotationTotalsDto>>;
