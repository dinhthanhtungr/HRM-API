using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Commands.SaveQuotation;

/// <summary>Save header, terms and line changes together without partially persisting a quotation.</summary>
public sealed record SaveQuotationCommand(Guid QuotationId, SaveQuotationRequest Request)
    : IRequest<OperationResult<QuotationTotalsDto>>;
