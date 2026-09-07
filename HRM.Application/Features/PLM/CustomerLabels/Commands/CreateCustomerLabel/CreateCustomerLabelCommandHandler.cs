using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.CustomerLabels.Dtos;
using HRM.Application.Features.PLM.CustomerLabels.Services;
using HRM.Domain.Entities.PrintectSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.CustomerLabels.Commands.CreateCustomerLabel;

internal sealed class CreateCustomerLabelCommandHandler : IRequestHandler<CreateCustomerLabelCommand, OperationResult<SaveCustomerLabelResultDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateCustomerLabelCommandHandler(IPLMWriteDbContext dbContext, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult<SaveCustomerLabelResultDto>> Handle(CreateCustomerLabelCommand request, CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId.GetValueOrDefault();
        var employeeId = _currentUser.EmployeeId.GetValueOrDefault();
        if (companyId == Guid.Empty || employeeId == Guid.Empty)
        {
            return OperationResult<SaveCustomerLabelResultDto>.Fail("Current employee or company is invalid.");
        }

        var validationError = Validate(request);
        if (validationError is not null)
        {
            return OperationResult<SaveCustomerLabelResultDto>.Fail(validationError);
        }

        var productExists = await _dbContext.Products.AsNoTracking().AnyAsync(product =>
            product.ProductId == request.ProductId && product.CompanyId == companyId && product.IsActive, cancellationToken);
        var customerExists = await _dbContext.Customers.AsNoTracking().AnyAsync(customer =>
            customer.CustomerId == request.CustomerId && customer.CompanyId == companyId && customer.IsActive == true, cancellationToken);
        if (!productExists || !customerExists)
        {
            return OperationResult<SaveCustomerLabelResultDto>.Fail("Product or customer was not found, is inactive, or is outside the current company.");
        }

        var now = _dateTimeProvider.Now;
        var entity = new CustomerLabelHeader
        {
            Id = Guid.CreateVersion7(),
            ProductId = request.ProductId,
            ColorCode = CustomerLabelRules.Normalize(request.ColorCode),
            CustomerId = request.CustomerId,
            CustomerExternalId = CustomerLabelRules.Normalize(request.CustomerExternalId),
            LabelType = CustomerLabelRules.Normalize(request.LabelType),
            IsActive = request.IsActive,
            CreatedBy = employeeId,
            UpdatedBy = employeeId,
            CreatedDate = now,
            UpdatedDate = now,
            Details = request.Details.Select(detail => new CustomerLabelDetail
            {
                Id = Guid.CreateVersion7(),
                LineNo = detail.LineNo,
                FieldKey = detail.FieldKey.Trim(),
                FieldValue = CustomerLabelRules.Normalize(detail.FieldValue),
                IsActive = detail.IsActive
            }).ToList()
        };

        await _dbContext.CustomerLabelHeaders.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<SaveCustomerLabelResultDto>.Ok(new SaveCustomerLabelResultDto
        {
            CustomerLabelHeaderId = entity.Id,
            UpdatedDate = entity.UpdatedDate
        }, "Created customer label successfully.");
    }

    private static string? Validate(CreateCustomerLabelCommand request)
    {
        if (request.ProductId == Guid.Empty) return "ProductId is invalid.";
        if (request.CustomerId == Guid.Empty) return "CustomerId is invalid.";
        if (request.Details is null) return "Details must be an array.";
        var textError = new[]
        {
            CustomerLabelRules.ValidateOptionalText(request.ColorCode, "ColorCode", CustomerLabelRules.MaxShortTextLength, false),
            CustomerLabelRules.ValidateOptionalText(request.CustomerExternalId, "CustomerExternalId", CustomerLabelRules.MaxShortTextLength, false),
            CustomerLabelRules.ValidateOptionalText(request.LabelType, "LabelType", CustomerLabelRules.MaxShortTextLength, false)
        }.FirstOrDefault(error => error is not null);
        if (textError is not null) return textError;

        foreach (var detail in request.Details)
        {
            if (string.IsNullOrWhiteSpace(detail.FieldKey))
            {
                return "Detail FieldKey must not be blank.";
            }

            var error = CustomerLabelRules.ValidateOptionalText(detail.FieldKey, "Detail FieldKey", CustomerLabelRules.MaxShortTextLength, true)
                ?? CustomerLabelRules.ValidateOptionalText(detail.FieldValue, "Detail FieldValue", CustomerLabelRules.MaxFieldValueLength, false);
            if (error is not null) return error;
        }

        var duplicateFieldKey = request.Details.GroupBy(detail => detail.FieldKey.Trim(), StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1);
        if (duplicateFieldKey) return "Detail FieldKey must be unique within a customer label.";

        return null;
    }
}
