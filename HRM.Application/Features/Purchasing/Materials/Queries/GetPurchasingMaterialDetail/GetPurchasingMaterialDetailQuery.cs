using HRM.Application.Features.Purchasing.Materials.Dtos;
using MediatR;

namespace HRM.Application.Features.Purchasing.Materials.Queries.GetPurchasingMaterialDetail;

public sealed record GetPurchasingMaterialDetailQuery(Guid MaterialId, bool IncludeInactive = false) : IRequest<PurchasingMaterialDetailDto?>;
