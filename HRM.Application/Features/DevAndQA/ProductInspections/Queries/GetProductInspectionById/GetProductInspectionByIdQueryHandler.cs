using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.DevAndQA.ProductInspections.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.ProductInspections.Queries.GetProductInspectionById;

internal sealed class GetProductInspectionByIdQueryHandler
    : IRequestHandler<GetProductInspectionByIdQuery, OperationResult<ProductInspectionDetailDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetProductInspectionByIdQueryHandler(IPLMReadDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<ProductInspectionDetailDto>> Handle(
        GetProductInspectionByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
            return OperationResult<ProductInspectionDetailDto>.Fail("Id is invalid.");

        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
            return OperationResult<ProductInspectionDetailDto>.Fail("Current company is invalid.");

        var query = _dbContext.ProductInspections.AsNoTracking()
            .ForCompany(_dbContext, companyId).Where(x => x.Id == request.Id);
        if (ImsAccountRestrictions.AppliesTo(_currentUser))
        {
            var tracking = await query.Select(x => new ProductInspectionDetailDto
            {
                Id = x.Id,
                ExternalId = x.ExternalId,
                BatchId = x.BatchId,
                ProductName = x.ProductName,
                ProductCode = x.ProductCode,
                Weight = x.Weight,
                ManufacturingDate = x.ManufacturingDate,
                ExpiryDate = x.ExpiryDate,
                DeliveryAccepted = x.DeliveryAccepted,
                CreateDate = x.CreateDate
            }).FirstOrDefaultAsync(cancellationToken);

            return ToResult(tracking);
        }

        var item = await query.Select(x => new ProductInspectionDetailDto
            {
                Id = x.Id,
                ProductId = x.ProductId,
                MfgProductionOrderId = x.MfgProductionOrderId,
                ExternalId = x.ExternalId,
                ProductStandardId = x.ProductStandardId,
                BatchId = x.BatchId,
                ProductName = x.ProductName,
                ProductCode = x.ProductCode,
                Weight = x.Weight,
                ManufacturingDate = x.ManufacturingDate,
                ExpiryDate = x.ExpiryDate,
                ExpiryType = _dbContext.ProductStandards
                    .Where(s => s.Id == x.ProductStandardId && s.CompanyId == companyId)
                    .Select(s => s.Product!.ExpiryType)
                    .FirstOrDefault(),
                Shape = x.Shape,
                IsShapePass = x.IsShapePass,
                ParticleSize = x.ParticleSize,
                IsParticleSizePass = x.IsParticleSizePass,
                PackingSpec = x.PackingSpec,
                IsPackingSpecPass = x.IsPackingSpecPass,
                VisualCheck = x.VisualCheck,
                ColorDeltaE = x.ColorDeltaE,
                IsColorDeltaEPass = x.IsColorDeltaEpass,
                Moisture = x.Moisture,
                IsMoisturePass = x.IsMoisturePass,
                Mfr = x.Mfr,
                IsMfrPass = x.IsMfrpass,
                FlexuralStrength = x.FlexuralStrength,
                IsFlexuralStrengthPass = x.IsFlexuralStrengthPass,
                Elongation = x.Elongation,
                IsElongationPass = x.IsElongationPass,
                Hardness = x.Hardness,
                IsHardnessPass = x.IsHardnessPass,
                Density = x.Density,
                IsDensityPass = x.IsDensityPass,
                TensileStrength = x.TensileStrength,
                IsTensileStrengthPass = x.IsTensileStrengthPass,
                FlexuralModulus = x.FlexuralModulus,
                IsFlexuralModulusPass = x.IsFlexuralModulusPass,
                ImpactResistance = x.ImpactResistance,
                IsImpactResistancePass = x.IsImpactResistancePass,
                Antistatic = x.Antistatic,
                IsAntistaticPass = x.IsAntistaticPass,
                StorageCondition = x.StorageCondition,
                IsStorageConditionPass = x.IsStorageConditionPass,
                IntrinsicViscosity = x.IntrinsicViscosity,
                IsIntrinsicViscosity = x.IsIntrinsicViscosity,
                MeshType = x.MeshType,
                IsMeshAttached = x.IsMeshAttached,
                DwellTime = x.DwellTime,
                BlackDots = x.BlackDots,
                MigrationTest = x.MigrationTest,
                DefectImpurity = x.DefectImpurity,
                DefectBlackDot = x.DefectBlackDot,
                DefectShortFiber = x.DefectShortFiber,
                DefectMoist = x.DefectMoist,
                DefectDusty = x.DefectDusty,
                DefectWrongColor = x.DefectWrongColor,
                Types = x.Types,
                DeliveryAccepted = x.DeliveryAccepted,
                Notes = x.Notes,
                CreateDate = x.CreateDate,
                CreatedBy = x.CreatedBy
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (item?.ProductStandardId is { } standardId)
            item.Specifications = await _dbContext.ProductStandards.AsNoTracking()
                .Where(x => x.Id == standardId && x.CompanyId == companyId)
                .Select(ProductInspectionQueries.Specifications).FirstOrDefaultAsync(cancellationToken);

        return ToResult(item);
    }

    private static OperationResult<ProductInspectionDetailDto> ToResult(ProductInspectionDetailDto? item) =>
        item is null
            ? OperationResult<ProductInspectionDetailDto>.Fail("Product inspection was not found or is outside your company.")
            : OperationResult<ProductInspectionDetailDto>.Ok(item);
}
