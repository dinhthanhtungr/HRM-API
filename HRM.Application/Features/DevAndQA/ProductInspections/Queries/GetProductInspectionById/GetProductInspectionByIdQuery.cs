using HRM.Application.Commons.Models;
using HRM.Application.Features.DevAndQA.ProductInspections.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.ProductInspections.Queries.GetProductInspectionById;

public sealed record GetProductInspectionByIdQuery(Guid Id)
    : IRequest<OperationResult<ProductInspectionDetailDto>>;
