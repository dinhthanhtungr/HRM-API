using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.ProductInspections.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.ProductInspections.Queries.GetProductInspections;

public sealed class GetProductInspectionsQuery
    : PaginationQuery, IRequest<OperationResult<PagedResult<ProductInspectionSummaryDto>>>;
