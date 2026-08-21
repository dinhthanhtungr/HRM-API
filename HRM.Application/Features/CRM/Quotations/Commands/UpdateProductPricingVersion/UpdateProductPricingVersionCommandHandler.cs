using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Commands.UpdateProductPricingVersion;

internal sealed class UpdateProductPricingVersionCommandHandler
    : IRequestHandler<UpdateProductPricingVersionCommand, OperationResult<ProductPricingVersionDto>>
{
    private readonly ICRMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ProductPricingSourceValidator _sourceValidator;

    public UpdateProductPricingVersionCommandHandler(
        ICRMWriteDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        ProductPricingSourceValidator sourceValidator)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _sourceValidator = sourceValidator;
    }

    public async Task<OperationResult<ProductPricingVersionDto>> Handle(
        UpdateProductPricingVersionCommand command,
        CancellationToken cancellationToken)
    {
        if (!ProductPricingAccessRules.CanManage(_currentUser))
            return OperationResult<ProductPricingVersionDto>.Fail("Only President or Developer can update product pricing versions.");

        if (command.ProductPricingVersionId == Guid.Empty ||
            _currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
            return OperationResult<ProductPricingVersionDto>.Fail("ProductPricingVersionId and current company/employee context are required.");

        var entity = await _dbContext.ProductPricingVersions
            .AsTracking()
            .Include(x => x.Product)
            .Include(x => x.PriceTiers)
            .Include(x => x.SourceFormula)
            .Include(x => x.SourceManufacturingFormula)
            .Include(x => x.SourceManufacturingVUFormula)
            .FirstOrDefaultAsync(x => x.ProductPricingVersionId == command.ProductPricingVersionId &&
                x.CompanyId == companyId && x.IsActive, cancellationToken);
        if (entity is null)
            return OperationResult<ProductPricingVersionDto>.Fail("Product pricing version was not found or is outside the current company.");
        if (entity.Status != ProductPricingStatus.Draft)
            return OperationResult<ProductPricingVersionDto>.Fail("Only a draft product pricing version can be updated.");

        var concurrencyError = OptimisticConcurrencyHelper.ValidateExpectedUpdatedDate(
            command.Request.ExpectedUpdatedDate, entity.UpdatedDate, "Product pricing version");
        if (concurrencyError is not null)
            return OperationResult<ProductPricingVersionDto>.Fail(concurrencyError);

        var sourceType = entity.SourceManufacturingFormulaId.HasValue
            ? ProductPricingSourceType.ManufacturingFormula
            : ProductPricingSourceType.Formula;
        var sourceId = entity.SourceManufacturingFormulaId ??
            entity.SourceFormulaId ??
            entity.SourceManufacturingVUFormula?.FormulaId ??
            Guid.Empty;
        var sourceResult = await _sourceValidator.ValidateAsync(
            entity.ProductId,
            companyId,
            entity.Currency,
            sourceType,
            sourceId,
            cancellationToken);
        if (!sourceResult.Success || sourceResult.Data is null)
            return OperationResult<ProductPricingVersionDto>.Fail(sourceResult.Message!);

        var changedField = ProductPricingVersionRules.ResolveChangedField(
            command.Request.ChangedField,
            entity.ManufacturingCost,
            entity.StandardSellingPrice,
            entity.ProfitMarginRate,
            command.Request.ManufacturingCost,
            command.Request.StandardSellingPrice,
            command.Request.ProfitMarginRate);
        var requestedProfitMarginRate = changedField == ProductPricingChangedField.ManufacturingCost
            ? command.Request.ProfitMarginRate ?? entity.ProfitMarginRate ?? 0m
            : command.Request.ProfitMarginRate;
        var pricingResult = ProductPricingVersionRules.NormalizePricingValues(
            sourceResult.Data.MaterialCostSnapshot,
            command.Request.ManufacturingCost,
            command.Request.StandardSellingPrice,
            requestedProfitMarginRate,
            changedField);
        if (!pricingResult.Success || pricingResult.Data is null)
            return OperationResult<ProductPricingVersionDto>.Fail(pricingResult.Message!);

        var tiersResult = ProductPricingVersionRules.BuildTiers(entity.ProductPricingVersionId, command.Request.PriceTiers);
        if (!tiersResult.Success || tiersResult.Data is null)
            return OperationResult<ProductPricingVersionDto>.Fail(tiersResult.Message!);

        _dbContext.ProductPricingTiers.RemoveRange(entity.PriceTiers);
        entity.HasManualTierAdjustment = true;
        entity.PriceTiers.Clear();
        foreach (var tier in tiersResult.Data) entity.PriceTiers.Add(tier);

        var now = _dateTimeProvider.Now;
        entity.MaterialCostSnapshot = pricingResult.Data.MaterialCostSnapshot;
        entity.ManufacturingCost = pricingResult.Data.ManufacturingCost;
        entity.StandardSellingPrice = pricingResult.Data.StandardSellingPrice;
        entity.ProfitMarginRate = pricingResult.Data.ProfitMarginRate;
        entity.CalculatedAt = now;
        entity.UpdatedBy = employeeId;
        entity.UpdatedDate = now;

        try { await _dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException exception)
        {
            return OperationResult<ProductPricingVersionDto>.Fail(
                OptimisticConcurrencyHelper.CreateConflictMessage("Product pricing version", exception));
        }

        return OperationResult<ProductPricingVersionDto>.Ok(
            ProductPricingVersionMapper.ToDto(entity), "Product pricing draft updated successfully.");
    }
}
