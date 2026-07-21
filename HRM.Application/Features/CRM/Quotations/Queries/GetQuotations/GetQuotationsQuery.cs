using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetQuotations;

public sealed class GetQuotationsQuery
    : PaginationQuery, IRequest<OperationResult<PagedResult<QuotationListItemDto>>>
{
    public Guid? CustomerId { get; init; }
    public Guid? SaleEmployeeId { get; init; }
    public QuotationStatus? Status { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}

