using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetQuotationById;

public sealed record GetQuotationByIdQuery(Guid QuotationId)
    : IRequest<OperationResult<QuotationDetailDto>>;

