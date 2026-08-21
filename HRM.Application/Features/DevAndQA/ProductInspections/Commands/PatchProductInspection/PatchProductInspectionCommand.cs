using HRM.Application.Commons.Models;
using HRM.Application.Features.DevAndQA.ProductInspections.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.ProductInspections.Commands.PatchProductInspection;

public sealed record PatchProductInspectionCommand(Guid Id, ProductInspectionWriteRequest Request)
    : IRequest<OperationResult<ProductInspectionWriteResultDto>>;
