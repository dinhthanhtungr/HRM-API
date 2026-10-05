using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.ProductionOrders.Commands.CreateProductionOrderInform;

/// <summary>Tạo MFG, VA tùy chọn, lịch và giữ chỗ trong cùng transaction.</summary>
public sealed record CreateProductionOrderInformCommand(CreateProductionOrderInformRequest Request)
    : IRequest<OperationResult<CreateProductionOrderInformResult>>;
