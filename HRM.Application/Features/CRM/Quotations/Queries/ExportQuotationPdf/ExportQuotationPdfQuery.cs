using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Queries.ExportQuotationPdf;

public sealed record ExportQuotationPdfQuery(Guid QuotationId)
    : IRequest<OperationResult<QuotationPdfFileDto>>;
