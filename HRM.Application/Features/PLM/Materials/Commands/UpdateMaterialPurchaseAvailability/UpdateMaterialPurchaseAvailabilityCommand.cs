using HRM.Application.Features.PLM.Materials.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Materials.Commands.UpdateMaterialPurchaseAvailability;

/// <summary>
/// Kế hoạch cập nhật khả năng mua NVL; lần chuyển sang Unavailable sẽ cảnh báo Lab.
/// </summary>
public sealed record UpdateMaterialPurchaseAvailabilityCommand(
    Guid MaterialId,
    UpdateMaterialPurchaseAvailabilityRequest Request)
    : IRequest<UpdateMaterialPurchaseAvailabilityResult>;
