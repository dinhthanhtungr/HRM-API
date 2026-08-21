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

namespace HRM.Application.Features.CRM.Quotations.Commands.ApproveProductPricingVersion;

internal sealed class ApproveProductPricingVersionCommandHandler
    : IRequestHandler<ApproveProductPricingVersionCommand, OperationResult<ProductPricingVersionDto>>
{
    private readonly ICRMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly KeyedMutationLock<Guid> _mutationLock;
    private readonly ProductPricingSourceValidator _sourceValidator;
    private readonly ProductPricingApprovalNotificationService _approvalNotificationService;

    public ApproveProductPricingVersionCommandHandler(
        ICRMWriteDbContext dbContext, ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider, KeyedMutationLock<Guid> mutationLock,
        ProductPricingSourceValidator sourceValidator,
        ProductPricingApprovalNotificationService approvalNotificationService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _mutationLock = mutationLock;
        _sourceValidator = sourceValidator;
        _approvalNotificationService = approvalNotificationService;
    }

    public async Task<OperationResult<ProductPricingVersionDto>> Handle(
        ApproveProductPricingVersionCommand command, CancellationToken cancellationToken)
    {
        if (!ProductPricingAccessRules.CanManage(_currentUser))
            return OperationResult<ProductPricingVersionDto>.Fail("Only President or Developer can approve product pricing versions.");
        if (command.ProductPricingVersionId == Guid.Empty ||
            _currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
            return OperationResult<ProductPricingVersionDto>.Fail("ProductPricingVersionId and current company/employee context are required.");

        var productId = await _dbContext.ProductPricingVersions.AsNoTracking()
            .Where(x => x.ProductPricingVersionId == command.ProductPricingVersionId &&
                x.CompanyId == companyId && x.IsActive)
            .Select(x => (Guid?)x.ProductId).FirstOrDefaultAsync(cancellationToken);
        if (!productId.HasValue)
            return OperationResult<ProductPricingVersionDto>.Fail("Product pricing version was not found or is outside the current company.");

        using var lease = await _mutationLock.AcquireAsync(productId.Value, cancellationToken);
        var entity = await _dbContext.ProductPricingVersions.AsTracking()
            .Include(x => x.Product)
            .Include(x => x.PriceTiers)
            .Include(x => x.SourceFormula)
            .Include(x => x.SourceManufacturingFormula)
            .Include(x => x.SourceManufacturingVUFormula)
            .FirstAsync(x => x.ProductPricingVersionId == command.ProductPricingVersionId, cancellationToken);
        if (entity.Status != ProductPricingStatus.Draft)
            return OperationResult<ProductPricingVersionDto>.Fail("Only a draft product pricing version can be approved.");

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
        {
            return OperationResult<ProductPricingVersionDto>.Fail(sourceResult.Message!);
        }

        var pricingResult = ProductPricingVersionRules.NormalizePricingValues(
            sourceResult.Data.MaterialCostSnapshot,
            entity.ManufacturingCost,
            entity.StandardSellingPrice,
            entity.ProfitMarginRate,
            ProductPricingChangedField.StandardSellingPrice);
        if (!pricingResult.Success || pricingResult.Data is null)
        {
            return OperationResult<ProductPricingVersionDto>.Fail(pricingResult.Message!);
        }

        if (!pricingResult.Data.MaterialCostSnapshot.HasValue ||
            !pricingResult.Data.ManufacturingCost.HasValue ||
            !pricingResult.Data.StandardSellingPrice.HasValue ||
            pricingResult.Data.StandardSellingPrice <= 0m ||
            !pricingResult.Data.ProfitMarginRate.HasValue ||
            entity.PriceTiers.Count == 0 ||
            entity.PriceTiers.Any(x => x.UnitPrice < 0m))
        {
            return OperationResult<ProductPricingVersionDto>.Fail(
                "Material cost, manufacturing cost, standard selling price, profit margin and non-negative price tiers are required before approval.");
        }

        var now = _dateTimeProvider.Now;
        var previousApproved = await _dbContext.ProductPricingVersions.AsTracking()
            .Where(x => x.ProductPricingVersionId != entity.ProductPricingVersionId &&
                x.CompanyId == companyId && x.ProductId == entity.ProductId &&
                x.Currency == entity.Currency && x.Status == ProductPricingStatus.Approved && x.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var previous in previousApproved)
        {
            previous.Status = ProductPricingStatus.Superseded;
            previous.UpdatedBy = employeeId;
            previous.UpdatedDate = now;
        }

        entity.MaterialCostSnapshot = pricingResult.Data.MaterialCostSnapshot;
        entity.ManufacturingCost = pricingResult.Data.ManufacturingCost;
        entity.StandardSellingPrice = pricingResult.Data.StandardSellingPrice;
        entity.ProfitMarginRate = pricingResult.Data.ProfitMarginRate;
        entity.CalculatedAt = now;
        entity.Status = ProductPricingStatus.Approved;
        entity.ApprovedBy = employeeId;
        entity.ApprovedAt = now;
        entity.UpdatedBy = employeeId;
        entity.UpdatedDate = now;

        try { await _dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException exception)
        {
            return OperationResult<ProductPricingVersionDto>.Fail(
                OptimisticConcurrencyHelper.CreateConflictMessage("Product pricing version", exception));
        }

        await _approvalNotificationService.PublishAsync(
            entity,
            companyId,
            employeeId,
            cancellationToken);

        return OperationResult<ProductPricingVersionDto>.Ok(
            ProductPricingVersionMapper.ToDto(entity), "Product pricing version approved successfully.");
    }

}
