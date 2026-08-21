using HRM.Application.Commons.Models;
using HRM.Application.Features.DevAndQA.ProductInspections.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.ProductInspections.Commands.CreateProductInspection;

public sealed record CreateProductInspectionCommand(ProductInspectionWriteRequest Request)
    : IRequest<OperationResult<ProductInspectionWriteResultDto>>;
