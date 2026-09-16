using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Queries.GetProductStandardBoms;

public sealed record GetProductStandardBomsQuery(Guid ProductId, bool History, DateTime? At = null)
    : IRequest<IReadOnlyList<ProductStandardBomVersionDto>>;
