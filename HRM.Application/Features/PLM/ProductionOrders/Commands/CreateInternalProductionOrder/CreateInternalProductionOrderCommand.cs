using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.ProductionOrders.Commands.CreateInternalProductionOrder;

/// <summary>Tạo MFG nội bộ theo flow cũ, cùng company và capability của người gọi.</summary>
public sealed record CreateInternalProductionOrderCommand(CreateInternalProductionOrderRequest Request)
    : IRequest<OperationResult<Guid>>;
