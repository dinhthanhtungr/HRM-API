using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.SaleOrders.Queries.ReviewStandardSamples;

public sealed record ReviewStandardSamplesQuery(CreateSaleOrderRequest Request)
    : IRequest<OperationResult<IReadOnlyList<StandardSampleReviewDto>>>;
