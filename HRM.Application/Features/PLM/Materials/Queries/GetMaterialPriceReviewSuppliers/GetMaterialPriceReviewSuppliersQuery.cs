using HRM.Application.Features.PLM.Materials.Dtos.PriceReview;
using MediatR;

namespace HRM.Application.Features.PLM.Materials.Queries.GetMaterialPriceReviewSuppliers;

public sealed record GetMaterialPriceReviewSuppliersQuery(Guid MaterialId)
    : IRequest<MaterialPriceReviewSuppliersDto?>;
