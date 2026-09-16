using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Materials.Dtos;
using HRM.Application.Features.PLM.Materials.Services;
using HRM.Domain.Entities.MaterialSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Materials.Commands.PatchMaterialSupplier;

/// <summary>
/// Chỉnh các thuộc tính được gửi hoặc ngừng/kích hoạt lại liên kết NVL - nhà cung cấp;
/// thay đổi/xác nhận giá luôn lưu PriceHistory.
/// </summary>
internal sealed class PatchMaterialSupplierCommandHandler
    : IRequestHandler<PatchMaterialSupplierCommand, OperationResult<MaterialSupplierManagementDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PatchMaterialSupplierCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult<MaterialSupplierManagementDto>> Handle(
        PatchMaterialSupplierCommand command,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateRequest(command.MaterialsSupplierId, command.Request);
        if (validationError is not null)
            return OperationResult<MaterialSupplierManagementDto>.Fail(validationError);

        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<MaterialSupplierManagementDto>.Fail(
                "Current company and employee context are required.");
        }

        var link = await _dbContext.MaterialsSuppliers
            .Include(x => x.Material)
            .Include(x => x.Supplier)
            .FirstOrDefaultAsync(x =>
                x.MaterialsSuppliersId == command.MaterialsSupplierId &&
                x.Material.IsActive == true &&
                x.Material.CompanyId == companyId &&
                x.Supplier.IsActive == true &&
                x.Supplier.CompanyId == companyId,
                cancellationToken);
        if (link is null)
        {
            return OperationResult<MaterialSupplierManagementDto>.Fail(
                "Material supplier was not found or is not accessible.");
        }

        var request = command.Request;
        if (request.IsPreferred == true && request.IsActive != true && link.IsActive != true)
        {
            return OperationResult<MaterialSupplierManagementDto>.Fail(
                "An inactive material supplier cannot be preferred.");
        }

        var expectedPrice = request.ExpectedCurrentPrice.HasValue
            ? MaterialSupplierManagementRules.RoundPrice(request.ExpectedCurrentPrice.Value)
            : (decimal?)null;
        if (expectedPrice.HasValue && link.CurrentPrice != expectedPrice.Value)
        {
            return OperationResult<MaterialSupplierManagementDto>.Fail(
                "The material supplier price has changed. Reload the latest value before updating.");
        }

        var now = _dateTimeProvider.Now;
        Guid? historyId = null;
        var oldPrice = link.CurrentPrice;
        var oldCurrency = link.Currency;
        var updatesPricing = request.NewPrice.HasValue || request.Currency is not null;
        if (updatesPricing)
        {
            historyId = Guid.NewGuid();
            _dbContext.PriceHistories.Add(new PriceHistory
            {
                PriceHistoryId = historyId.Value,
                MaterialsSuppliersId = link.MaterialsSuppliersId,
                OldPrice = oldPrice,
                Currency = oldCurrency,
                CreateDate = now,
                CreatedBy = employeeId
            });

            if (request.NewPrice.HasValue)
                link.CurrentPrice = MaterialSupplierManagementRules.RoundPrice(request.NewPrice.Value);
            if (request.Currency is not null)
                link.Currency = MaterialSupplierManagementRules.NormalizeCurrency(request.Currency);
        }

        var mustEnsureExclusivePreferred = request.IsPreferred == true ||
            (request.IsActive == true && !request.IsPreferred.HasValue && link.IsPreferred == true);
        if (mustEnsureExclusivePreferred)
        {
            var otherPreferredLinks = await _dbContext.MaterialsSuppliers
                .Where(x =>
                    x.MaterialId == link.MaterialId &&
                    x.MaterialsSuppliersId != link.MaterialsSuppliersId &&
                    x.IsActive == true &&
                    x.IsPreferred == true)
                .ToListAsync(cancellationToken);
            foreach (var preferred in otherPreferredLinks)
            {
                preferred.IsPreferred = false;
                preferred.UpdatedDate = now;
                preferred.UpdatedBy = employeeId;
            }
        }

        if (request.IsPreferred.HasValue)
            link.IsPreferred = request.IsPreferred.Value;

        if (request.IsActive.HasValue)
        {
            link.IsActive = request.IsActive.Value;
            if (!request.IsActive.Value)
                link.IsPreferred = false;
        }

        if (request.MinDeliveryDays.HasValue)
            link.MinDeliveryDays = request.MinDeliveryDays.Value;

        link.UpdatedDate = now;
        link.UpdatedBy = employeeId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var currentCurrency = link.Currency ?? string.Empty;
        return OperationResult<MaterialSupplierManagementDto>.Ok(new MaterialSupplierManagementDto
        {
            MaterialsSupplierId = link.MaterialsSuppliersId,
            MaterialId = link.MaterialId,
            MaterialCode = link.Material.ExternalId ?? string.Empty,
            MaterialName = link.Material.Name ?? string.Empty,
            SupplierId = link.SupplierId,
            SupplierCode = link.Supplier.ExternalId ?? string.Empty,
            SupplierName = link.Supplier.SupplierName ?? string.Empty,
            CurrentPrice = link.CurrentPrice,
            Currency = currentCurrency,
            IsPreferred = link.IsPreferred == true,
            IsActive = link.IsActive == true,
            MinDeliveryDays = link.MinDeliveryDays,
            UpdatedDate = now,
            UpdatedByEmployeeId = employeeId,
            PriceHistoryId = historyId,
            PriceChanged = oldPrice != link.CurrentPrice ||
                           !string.Equals(oldCurrency, currentCurrency, StringComparison.OrdinalIgnoreCase)
        });
    }

    internal static string? ValidateRequest(Guid materialsSupplierId, PatchMaterialSupplierRequest request)
    {
        if (materialsSupplierId == Guid.Empty)
            return "MaterialsSupplierId is required.";
        if (!request.NewPrice.HasValue && request.Currency is null &&
            !request.IsPreferred.HasValue && !request.MinDeliveryDays.HasValue &&
            !request.IsActive.HasValue)
        {
            return "At least one editable field is required.";
        }

        if (request.IsActive == false && request.IsPreferred == true)
            return "An inactive material supplier cannot be preferred.";

        return MaterialSupplierManagementRules.ValidatePrice(request.NewPrice, false, "NewPrice")
               ?? MaterialSupplierManagementRules.ValidatePrice(
                   request.ExpectedCurrentPrice, false, "ExpectedCurrentPrice")
               ?? MaterialSupplierManagementRules.ValidateCurrency(request.Currency)
               ?? MaterialSupplierManagementRules.ValidateDeliveryDays(request.MinDeliveryDays);
    }
}
