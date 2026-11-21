using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Commons.ExternalIds;
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
    private readonly IExternalIdService _externalIds;

    public CreateCustomerLabelCommandHandler(IPLMWriteDbContext dbContext, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider, IExternalIdService externalIds)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _externalIds = externalIds;
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

        var product = await _dbContext.Products.AsNoTracking().Where(p =>
            p.ProductId == request.ProductId && p.CompanyId == companyId && p.IsActive)
            .Select(p => new { p.ColourCode }).SingleOrDefaultAsync(cancellationToken);
        var customer = await _dbContext.Customers.AsNoTracking().Where(c =>
            c.CustomerId == request.CustomerId && c.CompanyId == companyId && c.IsActive == true)
            .Select(c => new { c.ExternalId }).SingleOrDefaultAsync(cancellationToken);
        if (product is null || customer is null)
        {
            return OperationResult<SaveCustomerLabelResultDto>.Fail("Product or customer was not found, is inactive, or is outside the current company.");
        }

        if (request.DefaultPrintLabelLogoId.HasValue && !request.PrintLabelTemplateId.HasValue)
        {
            return OperationResult<SaveCustomerLabelResultDto>.Fail("DefaultPrintLabelLogoId requires PrintLabelTemplateId.");
        }

        if (request.PrintLabelTemplateId.HasValue)
        {
            var templateExists = await _dbContext.PrintLabelTemplates.AsNoTracking().AnyAsync(template =>
                template.Id == request.PrintLabelTemplateId.Value && template.CompanyId == companyId && template.IsActive, cancellationToken);
            if (!templateExists)
                return OperationResult<SaveCustomerLabelResultDto>.Fail("Print label template was not found, inactive, or is outside the current company.");

            if (request.DefaultPrintLabelLogoId.HasValue)
            {
                var logoAllowed = await _dbContext.PrintLabelTemplateLogos.AsNoTracking().AnyAsync(link =>
                    link.PrintLabelTemplateId == request.PrintLabelTemplateId.Value &&
                    link.PrintLabelLogoId == request.DefaultPrintLabelLogoId.Value && link.IsActive &&
                    link.Logo.CompanyId == companyId && link.Logo.IsActive, cancellationToken);
                if (!logoAllowed)
                    return OperationResult<SaveCustomerLabelResultDto>.Fail("The default logo is not active or is not allowed by the selected print label template.");
            }

            var elementIds = request.Details.Where(detail => detail.PrintLabelElementId.HasValue)
                .Select(detail => detail.PrintLabelElementId!.Value).Distinct().ToArray();
            if (elementIds.Length > 0)
            {
                var linkedElementCount = await _dbContext.PrintLabelElements.AsNoTracking().CountAsync(element =>
                    element.PrintLabelTemplateId == request.PrintLabelTemplateId.Value && elementIds.Contains(element.Id), cancellationToken);
                if (linkedElementCount != elementIds.Length)
                    return OperationResult<SaveCustomerLabelResultDto>.Fail("A customer label detail references an element outside the selected print label template.");
            }
        }
        else if (request.Details.Any(detail => detail.PrintLabelElementId.HasValue))
        {
            return OperationResult<SaveCustomerLabelResultDto>.Fail("PrintLabelElementId requires PrintLabelTemplateId.");
        }

        var colorCode = CustomerLabelRules.Normalize(product.ColourCode);
        if (colorCode is null)
        {
            var customerCode = CustomerLabelRules.Normalize(customer.ExternalId);
            if (customerCode is null) return OperationResult<SaveCustomerLabelResultDto>.Fail("Customer code is required to allocate a temporary label code.");
            // Persistent atomic counter: replacing the temporary code never reuses its number.
            var allocated = await _externalIds.GenerateGlobalCodeAsync(companyId,
                $"CL:{companyId:N}:{request.CustomerId:N}", cancellationToken);
            colorCode = $"{customerCode}_{allocated[(allocated.LastIndexOf('_') + 1)..]}";
        }
        var now = _dateTimeProvider.Now;
        var entity = new CustomerLabelHeader
        {
            Id = Guid.CreateVersion7(),
            ProductId = request.ProductId,
            ColorCode = colorCode,
            CustomerId = request.CustomerId,
            CustomerExternalId = CustomerLabelRules.Normalize(customer.ExternalId),
            LabelType = CustomerLabelRules.Normalize(request.LabelType),
            PrintLabelTemplateId = request.PrintLabelTemplateId,
            DefaultPrintLabelLogoId = request.DefaultPrintLabelLogoId,
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
                PrintLabelElementId = detail.PrintLabelElementId,
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
