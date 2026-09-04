using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Materials.Dtos;
using HRM.Domain.Entities.MaterialSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Materials.Commands.UpdateMaterialSupplierPrice;

/// <summary>
/// Cập nhật giá hiện tại của một liên kết NVL - nhà cung cấp và lưu giá cũ vào PriceHistory.
/// Hai thay đổi được ghi trong cùng một lần SaveChanges để bảo đảm tính nguyên tử.
/// </summary>
internal sealed class UpdateMaterialSupplierPriceCommandHandler
    : IRequestHandler<UpdateMaterialSupplierPriceCommand,
        OperationResult<UpdateMaterialSupplierPriceResultDto>>
{
    private const decimal MaxSupportedPrice = 99_999_999_999_999.9999m;
    private const int MaxCurrencyLength = 10;

    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdateMaterialSupplierPriceCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult<UpdateMaterialSupplierPriceResultDto>> Handle(
        UpdateMaterialSupplierPriceCommand command,
        CancellationToken cancellationToken)
    {
        if (command.MaterialsSupplierId == Guid.Empty)
        {
            return OperationResult<UpdateMaterialSupplierPriceResultDto>.Fail(
                "MaterialsSupplierId is required.");
        }

        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return OperationResult<UpdateMaterialSupplierPriceResultDto>.Fail(
                "Current user does not have a company context.");
        }

        if (_currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<UpdateMaterialSupplierPriceResultDto>.Fail(
                "Current user does not have an employee profile.");
        }

        var validationError = ValidateRequest(command.Request);
        if (validationError is not null)
        {
            return OperationResult<UpdateMaterialSupplierPriceResultDto>.Fail(validationError);
        }

        var materialSupplier = await _dbContext.MaterialsSuppliers
            .Include(x => x.Material)
            .Include(x => x.Supplier)
            .Where(x =>
                x.MaterialsSuppliersId == command.MaterialsSupplierId &&
                x.IsActive == true &&
                x.Material.IsActive == true &&
                x.Material.CompanyId == companyId &&
                x.Supplier.IsActive == true &&
                x.Supplier.CompanyId == companyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (materialSupplier is null)
        {
            return OperationResult<UpdateMaterialSupplierPriceResultDto>.Fail(
                "Material supplier price was not found or is not accessible.");
        }

        var newPrice = decimal.Round(command.Request.NewPrice, 4, MidpointRounding.AwayFromZero);
        var newCurrency = NormalizeCurrency(command.Request.Currency, materialSupplier.Currency);

        var expectedCurrentPrice = command.Request.ExpectedCurrentPrice.HasValue
            ? decimal.Round(command.Request.ExpectedCurrentPrice.Value, 4, MidpointRounding.AwayFromZero)
            : (decimal?)null;

        if (expectedCurrentPrice.HasValue &&
            materialSupplier.CurrentPrice != expectedCurrentPrice.Value)
        {
            return OperationResult<UpdateMaterialSupplierPriceResultDto>.Fail(
                "The material supplier price has changed. Reload the latest value before updating.");
        }

        if (materialSupplier.CurrentPrice == newPrice &&
            string.Equals(materialSupplier.Currency, newCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<UpdateMaterialSupplierPriceResultDto>.Fail(
                "The new price and currency are the same as the current values.");
        }

        var oldPrice = materialSupplier.CurrentPrice;
        var oldCurrency = materialSupplier.Currency;
        var now = _dateTimeProvider.Now;
        var historyId = Guid.NewGuid();

        _dbContext.PriceHistories.Add(new PriceHistory
        {
            PriceHistoryId = historyId,
            MaterialsSuppliersId = materialSupplier.MaterialsSuppliersId,
            OldPrice = oldPrice,
            Currency = oldCurrency,
            CreateDate = now,
            CreatedBy = employeeId
        });

        materialSupplier.CurrentPrice = newPrice;
        materialSupplier.Currency = newCurrency;
        materialSupplier.UpdatedDate = now;
        materialSupplier.UpdatedBy = employeeId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<UpdateMaterialSupplierPriceResultDto>.Ok(
            new UpdateMaterialSupplierPriceResultDto
            {
                MaterialsSupplierId = materialSupplier.MaterialsSuppliersId,
                MaterialId = materialSupplier.MaterialId,
                MaterialCode = materialSupplier.Material.ExternalId ?? string.Empty,
                MaterialName = materialSupplier.Material.Name ?? string.Empty,
                SupplierId = materialSupplier.SupplierId,
                SupplierCode = materialSupplier.Supplier.ExternalId ?? string.Empty,
                SupplierName = materialSupplier.Supplier.SupplierName ?? string.Empty,
                OldPrice = oldPrice,
                NewPrice = newPrice,
                Currency = newCurrency,
                PriceHistoryId = historyId,
                UpdatedDate = now,
                UpdatedByEmployeeId = employeeId
            });
    }

    private static string? ValidateRequest(UpdateMaterialSupplierPriceRequest request)
    {
        if (request.NewPrice < 0m)
        {
            return "NewPrice cannot be negative.";
        }

        if (request.NewPrice > MaxSupportedPrice)
        {
            return $"NewPrice cannot exceed {MaxSupportedPrice}.";
        }

        if (request.ExpectedCurrentPrice is < 0m)
        {
            return "ExpectedCurrentPrice cannot be negative.";
        }

        if (request.Currency is { Length: > 0 } currency &&
            currency.Trim().Length > MaxCurrencyLength)
        {
            return $"Currency cannot exceed {MaxCurrencyLength} characters.";
        }

        if (request.Currency is not null && string.IsNullOrWhiteSpace(request.Currency))
        {
            return "Currency cannot be blank.";
        }

        return null;
    }

    private static string? NormalizeCurrency(string? requestedCurrency, string? currentCurrency)
    {
        return requestedCurrency is null
            ? currentCurrency
            : requestedCurrency.Trim().ToUpperInvariant();
    }
}
