using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Materials.Dtos;
using HRM.Application.Features.PLM.Materials.Services;
using HRM.Domain.Entities.MaterialSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Materials.Commands.CreateMaterialSupplier;

/// <summary>
/// Gắn một nhà cung cấp cùng công ty vào NVL active và lưu giá ban đầu để màn hình rà giá dùng ngay.
/// </summary>
internal sealed class CreateMaterialSupplierCommandHandler
    : IRequestHandler<CreateMaterialSupplierCommand, OperationResult<MaterialSupplierManagementDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateMaterialSupplierCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult<MaterialSupplierManagementDto>> Handle(
        CreateMaterialSupplierCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        var validationError = ValidateRequest(request);
        if (validationError is not null)
            return OperationResult<MaterialSupplierManagementDto>.Fail(validationError);

        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<MaterialSupplierManagementDto>.Fail(
                "Current company and employee context are required.");
        }

        var material = await _dbContext.Materials
            .FirstOrDefaultAsync(x =>
                x.MaterialId == request.MaterialId &&
                x.CompanyId == companyId &&
                x.IsActive == true,
                cancellationToken);
        if (material is null)
            return OperationResult<MaterialSupplierManagementDto>.Fail("Material was not found or is not accessible.");

        var supplier = await _dbContext.Suppliers
            .FirstOrDefaultAsync(x =>
                x.SupplierId == request.SupplierId &&
                x.CompanyId == companyId &&
                x.IsActive == true,
                cancellationToken);
        if (supplier is null)
            return OperationResult<MaterialSupplierManagementDto>.Fail("Supplier was not found or is not accessible.");

        var existingLinks = await _dbContext.MaterialsSuppliers
            .Where(x => x.MaterialId == request.MaterialId)
            .ToListAsync(cancellationToken);
        if (existingLinks.Any(x => x.SupplierId == request.SupplierId && x.IsActive == true))
            return OperationResult<MaterialSupplierManagementDto>.Fail("Supplier is already linked to this material.");

        var now = _dateTimeProvider.Now;
        if (request.IsPreferred)
        {
            foreach (var preferred in existingLinks.Where(x => x.IsPreferred == true))
            {
                preferred.IsPreferred = false;
                preferred.UpdatedDate = now;
                preferred.UpdatedBy = employeeId;
            }
        }

        var link = existingLinks
            .Where(x => x.SupplierId == request.SupplierId && x.IsActive != true)
            .OrderByDescending(x => x.UpdatedDate ?? x.CreateDate)
            .FirstOrDefault();
        var price = MaterialSupplierManagementRules.RoundPrice(request.CurrentPrice!.Value);
        var currency = MaterialSupplierManagementRules.NormalizeCurrency(request.Currency);

        if (link is null)
        {
            link = new MaterialsSupplier
            {
                MaterialsSuppliersId = Guid.NewGuid(),
                MaterialId = request.MaterialId,
                SupplierId = request.SupplierId,
                CreateDate = now,
                CreatedBy = employeeId
            };
            _dbContext.MaterialsSuppliers.Add(link);
        }

        link.CurrentPrice = price;
        link.Currency = currency;
        link.MinDeliveryDays = request.MinDeliveryDays;
        link.IsPreferred = request.IsPreferred;
        link.IsActive = true;
        link.UpdatedDate = now;
        link.UpdatedBy = employeeId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<MaterialSupplierManagementDto>.Ok(new MaterialSupplierManagementDto
        {
            MaterialsSupplierId = link.MaterialsSuppliersId,
            MaterialId = material.MaterialId,
            MaterialCode = material.ExternalId ?? string.Empty,
            MaterialName = material.Name ?? string.Empty,
            SupplierId = supplier.SupplierId,
            SupplierCode = supplier.ExternalId ?? string.Empty,
            SupplierName = supplier.SupplierName ?? string.Empty,
            CurrentPrice = link.CurrentPrice,
            Currency = link.Currency ?? string.Empty,
            IsPreferred = link.IsPreferred == true,
            IsActive = link.IsActive == true,
            MinDeliveryDays = link.MinDeliveryDays,
            UpdatedDate = now,
            UpdatedByEmployeeId = employeeId,
            PriceChanged = true
        });
    }

    internal static string? ValidateRequest(CreateMaterialSupplierRequest request)
    {
        if (request.MaterialId == Guid.Empty)
            return "MaterialId is required.";
        if (request.SupplierId == Guid.Empty)
            return "SupplierId is required.";

        return MaterialSupplierManagementRules.ValidatePrice(request.CurrentPrice, true, "CurrentPrice")
               ?? MaterialSupplierManagementRules.ValidateCurrency(request.Currency)
               ?? MaterialSupplierManagementRules.ValidateDeliveryDays(request.MinDeliveryDays);
    }
}
