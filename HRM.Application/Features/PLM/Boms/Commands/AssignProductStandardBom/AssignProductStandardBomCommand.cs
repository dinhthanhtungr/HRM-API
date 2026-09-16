using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.AssignProductStandardBom;

public sealed record AssignProductStandardBomCommand(
    Guid ProductId,
    AssignProductStandardBomRequest Request)
    : IRequest<OperationResult<ProductStandardBomVersionDto>>;
