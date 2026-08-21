using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Commands.CreateProductPricingVersion;

internal sealed class CreateProductPricingVersionCommandHandler
    : IRequestHandler<CreateProductPricingVersionCommand, OperationResult<ProductPricingVersionDto>>
{
    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ProductPricingSourceValidator _sourceValidator;
    private readonly FormulaPricingPolicyProvider _pricingPolicyProvider;
    private readonly KeyedMutationLock<Guid> _mutationLock;
    private readonly ProductPricingApprovalNotificationService _approvalNotificationService;

    public CreateProductPricingVersionCommandHandler(
        ICRMReadDbContext readDbContext,
        ICRMWriteDbContext writeDbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        ProductPricingSourceValidator sourceValidator,
        FormulaPricingPolicyProvider pricingPolicyProvider,
        KeyedMutationLock<Guid> mutationLock,
        ProductPricingApprovalNotificationService approvalNotificationService)
    {
        _readDbContext = readDbContext;
        _writeDbContext = writeDbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _sourceValidator = sourceValidator;
        _pricingPolicyProvider = pricingPolicyProvider;
        _mutationLock = mutationLock;
        _approvalNotificationService = approvalNotificationService;
    }

    public async Task<OperationResult<ProductPricingVersionDto>> Handle(
        CreateProductPricingVersionCommand command,
        CancellationToken cancellationToken)
    {
        if (!ProductPricingAccessRules.CanManage(_currentUser))
        {
            return OperationResult<ProductPricingVersionDto>.Fail(
                "Only President or Developer can create product pricing versions.");
        }

        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty ||
            command.Request.ProductId == Guid.Empty)
        {
            return OperationResult<ProductPricingVersionDto>.Fail(
                "ProductId and current company/employee context are required.");
        }

        var currency = QuotationRules.TrimToNull(command.Request.Currency)?.ToUpperInvariant();
        if (currency is null || currency.Length > QuotationRules.MaximumCurrencyLength)
        {
            return OperationResult<ProductPricingVersionDto>.Fail(
                $"Currency is required and cannot exceed {QuotationRules.MaximumCurrencyLength} characters.");
        }

        var productInfo = await _readDbContext.Products
            .AsNoTracking()
            .Where(x =>
                x.ProductId == command.Request.ProductId &&
                x.CompanyId == companyId &&
                x.IsActive)
            .Select(x => new
            {
                x.FormulaPricingProfile
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (productInfo is null)
        {
            return OperationResult<ProductPricingVersionDto>.Fail(
                "Product was not found, inactive, or outside the current company.");
        }

        var sourceResult = await _sourceValidator.ValidateAsync(
            command.Request.ProductId,
            companyId,
            currency,
            command.Request.SourceType,
            command.Request.SourceId,
            cancellationToken);
        if (!sourceResult.Success || sourceResult.Data is null)
        {
            return OperationResult<ProductPricingVersionDto>.Fail(sourceResult.Message!);
        }

        if (sourceResult.Data.PricingStatus == FormulaPricingPolicyRules.PricingPolicyMissing ||
            !sourceResult.Data.FormulaPricingPolicyId.HasValue)
        {
            return OperationResult<ProductPricingVersionDto>.Fail(
                FormulaPricingPolicyRules.PricingPolicyMissing);
        }

        if (!sourceResult.Data.IsMaterialCostComplete ||
            !sourceResult.Data.MaterialCostSnapshot.HasValue)
        {
            return OperationResult<ProductPricingVersionDto>.Fail("MaterialPriceMissing");
        }

        if (productInfo.FormulaPricingProfile is not { } pricingProfile ||
            !Enum.IsDefined(pricingProfile))
        {
            return OperationResult<ProductPricingVersionDto>.Fail(
                "Pricing profile is not configured for this product.");
        }

        var pricingPolicy = await _pricingPolicyProvider.GetPublishedPolicyAsync(
            companyId,
            pricingProfile,
            currency,
            cancellationToken);
        if (pricingPolicy is null ||
            pricingPolicy.FormulaPricingPolicyId != sourceResult.Data.FormulaPricingPolicyId)
        {
            return OperationResult<ProductPricingVersionDto>.Fail(
                FormulaPricingPolicyRules.PricingPolicyMissing);
        }

        var pricingResult = ProductPricingVersionPolicyRules.Calculate(
            pricingPolicy.Definition,
            sourceResult.Data.MaterialCostSnapshot,
            command.Request.ManufacturingCost,
            command.Request.StandardSellingPrice,
            command.Request.ProfitMarginRate,
            command.Request.ChangedField);
        if (!pricingResult.Success || pricingResult.Data is null)
        {
            return OperationResult<ProductPricingVersionDto>.Fail(pricingResult.Message!);
        }

        var id = Guid.CreateVersion7();
        var tiersResult = ProductPricingVersionPolicyRules.BuildPolicyTiers(
            id,
            pricingResult.Data.SuggestedPriceTiers,
            command.Request.PriceTiers);
        if (!tiersResult.Success || tiersResult.Data is null)
        {
            return OperationResult<ProductPricingVersionDto>.Fail(tiersResult.Message!);
        }

        if (command.Request.ApproveImmediately &&
            (pricingResult.Data.StandardSellingPrice <= 0m ||
             tiersResult.Data.Tiers.Count == 0))
        {
            return OperationResult<ProductPricingVersionDto>.Fail(
                "Material cost, manufacturing cost, standard selling price, profit margin and non-negative price tiers are required before approval.");
        }

        using var lease = await _mutationLock.AcquireAsync(
            command.Request.ProductId,
            cancellationToken);
        var latestVersion = await _writeDbContext.ProductPricingVersions
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.ProductId == command.Request.ProductId &&
                x.Currency == currency)
            .MaxAsync(x => (int?)x.Version, cancellationToken) ?? 0;
        var now = _dateTimeProvider.Now;
        if (command.Request.ApproveImmediately)
        {
            var previousCurrentVersions = await _writeDbContext.ProductPricingVersions
                .AsTracking()
                .Where(x =>
                    x.CompanyId == companyId &&
                    x.ProductId == command.Request.ProductId &&
                    x.Currency == currency &&
                    (x.Status == ProductPricingStatus.Approved ||
                     x.Status == ProductPricingStatus.Draft) &&
                    x.IsActive)
                .ToListAsync(cancellationToken);
            foreach (var previous in previousCurrentVersions)
            {
                previous.Status = previous.Status == ProductPricingStatus.Approved
                    ? ProductPricingStatus.Superseded
                    : ProductPricingStatus.Cancelled;
                previous.UpdatedBy = employeeId;
                previous.UpdatedDate = now;
            }
        }

        var entity = new ProductPricingVersion
        {
            ProductPricingVersionId = id,
            CompanyId = companyId,
            ProductId = command.Request.ProductId,
            FormulaPricingPolicyId = pricingPolicy.FormulaPricingPolicyId,
            HasManualTierAdjustment = tiersResult.Data.HasManualTierAdjustment,
            SourceFormulaId = sourceResult.Data.FormulaId,
            SourceManufacturingFormulaId = sourceResult.Data.ManufacturingFormulaId,
            FormulaExternalIdSnapshot = sourceResult.Data.ExternalId,
            Currency = currency,
            MaterialCostSnapshot = pricingResult.Data.MaterialCost,
            ManufacturingCost = pricingResult.Data.ManufacturingCost,
            StandardSellingPrice = pricingResult.Data.StandardSellingPrice,
            ProfitMarginRate = pricingResult.Data.ProfitMarginRate,
            Status = command.Request.ApproveImmediately
                ? ProductPricingStatus.Approved
                : ProductPricingStatus.Draft,
            Version = latestVersion + 1,
            CalculatedAt = now,
            IsActive = true,
            CreatedBy = employeeId,
            CreatedDate = now,
            UpdatedBy = employeeId,
            UpdatedDate = now,
            ApprovedBy = command.Request.ApproveImmediately ? employeeId : null,
            ApprovedAt = command.Request.ApproveImmediately ? now : null,
            PriceTiers = tiersResult.Data.Tiers.ToList()
        };

        await _writeDbContext.ProductPricingVersions.AddAsync(entity, cancellationToken);
        try
        {
            await _writeDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            return OperationResult<ProductPricingVersionDto>.Fail(
                OptimisticConcurrencyHelper.CreateConflictMessage(
                    "Product pricing version",
                    exception));
        }
        catch (DbUpdateException)
        {
            return OperationResult<ProductPricingVersionDto>.Fail(
                "Product pricing version could not be created. Reload and try again.");
        }

        var saved = await _readDbContext.ProductPricingVersions
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.PriceTiers)
            .Include(x => x.FormulaPricingPolicy)
            .Include(x => x.SourceFormula)
            .Include(x => x.SourceManufacturingFormula)
            .FirstAsync(x => x.ProductPricingVersionId == id, cancellationToken);

        if (command.Request.ApproveImmediately)
        {
            await _approvalNotificationService.PublishAsync(
                saved,
                companyId,
                employeeId,
                cancellationToken);
        }

        return OperationResult<ProductPricingVersionDto>.Ok(
            ProductPricingVersionMapper.ToDto(saved),
            command.Request.ApproveImmediately
                ? "Product pricing version created and approved successfully."
                : "Product pricing draft created successfully.");
    }
}
