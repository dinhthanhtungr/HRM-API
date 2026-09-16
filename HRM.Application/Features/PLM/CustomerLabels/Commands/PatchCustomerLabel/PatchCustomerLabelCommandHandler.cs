using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.PLM.CustomerLabels.Dtos;
using HRM.Application.Features.PLM.CustomerLabels.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.CustomerLabels.Commands.PatchCustomerLabel;

internal sealed class PatchCustomerLabelCommandHandler : IRequestHandler<PatchCustomerLabelCommand, OperationResult<SaveCustomerLabelResultDto>>
{
    private static readonly HashSet<string> ClearableFields = new(StringComparer.OrdinalIgnoreCase) { "colorCode", "customerExternalId", "labelType", "printLabelTemplateId", "defaultPrintLabelLogoId" };
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PatchCustomerLabelCommandHandler(IPLMWriteDbContext dbContext, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult<SaveCustomerLabelResultDto>> Handle(PatchCustomerLabelCommand request, CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId.GetValueOrDefault();
        var employeeId = _currentUser.EmployeeId.GetValueOrDefault();
        if (request.CustomerLabelHeaderId == Guid.Empty || companyId == Guid.Empty || employeeId == Guid.Empty)
            return OperationResult<SaveCustomerLabelResultDto>.Fail("CustomerLabelHeaderId or current user is invalid.");

        var clearFields = request.ClearFields ?? Array.Empty<string>();
        if (clearFields.Any(field => string.IsNullOrWhiteSpace(field) || !ClearableFields.Contains(field)) ||
            clearFields.Distinct(StringComparer.OrdinalIgnoreCase).Count() != clearFields.Count)
            return OperationResult<SaveCustomerLabelResultDto>.Fail("ClearFields contains an unsupported or duplicate field.");
        if ((request.ColorCode is not null && clearFields.Contains("colorCode", StringComparer.OrdinalIgnoreCase)) ||
            (request.CustomerExternalId is not null && clearFields.Contains("customerExternalId", StringComparer.OrdinalIgnoreCase)) ||
            (request.LabelType is not null && clearFields.Contains("labelType", StringComparer.OrdinalIgnoreCase)) ||
            (request.PrintLabelTemplateId.HasValue && clearFields.Contains("printLabelTemplateId", StringComparer.OrdinalIgnoreCase)) ||
            (request.DefaultPrintLabelLogoId.HasValue && clearFields.Contains("defaultPrintLabelLogoId", StringComparer.OrdinalIgnoreCase)))
            return OperationResult<SaveCustomerLabelResultDto>.Fail("A field cannot be both updated and cleared.");

        var validationError = new[]
        {
            CustomerLabelRules.ValidateOptionalText(request.ColorCode, "ColorCode", CustomerLabelRules.MaxShortTextLength, true),
            CustomerLabelRules.ValidateOptionalText(request.CustomerExternalId, "CustomerExternalId", CustomerLabelRules.MaxShortTextLength, true),
            CustomerLabelRules.ValidateOptionalText(request.LabelType, "LabelType", CustomerLabelRules.MaxShortTextLength, true)
        }.FirstOrDefault(error => error is not null);
        if (validationError is not null) return OperationResult<SaveCustomerLabelResultDto>.Fail(validationError);

        var entity = await _dbContext.CustomerLabelHeaders.FirstOrDefaultAsync(header =>
            header.Id == request.CustomerLabelHeaderId && header.Product.CompanyId == companyId && header.Customer.CompanyId == companyId,
            cancellationToken);
        if (entity is null) return OperationResult<SaveCustomerLabelResultDto>.Fail("Customer label was not found or is outside the current company.");

        var templateId = clearFields.Contains("printLabelTemplateId", StringComparer.OrdinalIgnoreCase)
            ? (Guid?)null
            : request.PrintLabelTemplateId ?? entity.PrintLabelTemplateId;
        var logoId = clearFields.Contains("defaultPrintLabelLogoId", StringComparer.OrdinalIgnoreCase)
            ? (Guid?)null
            : request.DefaultPrintLabelLogoId ?? entity.DefaultPrintLabelLogoId;
        if (logoId.HasValue && !templateId.HasValue)
            return OperationResult<SaveCustomerLabelResultDto>.Fail("DefaultPrintLabelLogoId requires PrintLabelTemplateId.");
        if (templateId.HasValue)
        {
            var templateExists = await _dbContext.PrintLabelTemplates.AsNoTracking().AnyAsync(template =>
                template.Id == templateId.Value && template.CompanyId == companyId && template.IsActive, cancellationToken);
            if (!templateExists) return OperationResult<SaveCustomerLabelResultDto>.Fail("Print label template was not found, inactive, or is outside the current company.");
            if (logoId.HasValue)
            {
                var logoAllowed = await _dbContext.PrintLabelTemplateLogos.AsNoTracking().AnyAsync(link =>
                    link.PrintLabelTemplateId == templateId.Value && link.PrintLabelLogoId == logoId.Value && link.IsActive &&
                    link.Logo.CompanyId == companyId && link.Logo.IsActive, cancellationToken);
                if (!logoAllowed) return OperationResult<SaveCustomerLabelResultDto>.Fail("The default logo is not active or is not allowed by the selected print label template.");
            }
        }

        var changed = PatchHelper.SetTrimmed(request.ColorCode, () => entity.ColorCode, value => entity.ColorCode = value)
            | PatchHelper.SetTrimmed(request.CustomerExternalId, () => entity.CustomerExternalId, value => entity.CustomerExternalId = value)
            | PatchHelper.SetTrimmed(request.LabelType, () => entity.LabelType, value => entity.LabelType = value)
            | SetOptionalGuid(request.PrintLabelTemplateId, () => entity.PrintLabelTemplateId, value => entity.PrintLabelTemplateId = value)
            | SetOptionalGuid(request.DefaultPrintLabelLogoId, () => entity.DefaultPrintLabelLogoId, value => entity.DefaultPrintLabelLogoId = value)
            | PatchHelper.SetIfHasValue(request.IsActive, () => entity.IsActive, value => entity.IsActive = value);
        foreach (var field in clearFields)
        {
            changed |= field.ToLowerInvariant() switch
            {
                "colorcode" => PatchHelper.SetNullableRef<string>(null, () => entity.ColorCode, value => entity.ColorCode = value),
                "customerexternalid" => PatchHelper.SetNullableRef<string>(null, () => entity.CustomerExternalId, value => entity.CustomerExternalId = value),
                "labeltype" => PatchHelper.SetNullableRef<string>(null, () => entity.LabelType, value => entity.LabelType = value),
                "printlabeltemplateid" => PatchHelper.SetNullable<Guid>(null, () => entity.PrintLabelTemplateId, value => entity.PrintLabelTemplateId = value),
                "defaultprintlabellogoid" => PatchHelper.SetNullable<Guid>(null, () => entity.DefaultPrintLabelLogoId, value => entity.DefaultPrintLabelLogoId = value),
                _ => false
            };
        }

        if (changed)
        {
            entity.UpdatedBy = employeeId;
            entity.UpdatedDate = _dateTimeProvider.Now;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return OperationResult<SaveCustomerLabelResultDto>.Ok(new SaveCustomerLabelResultDto
        {
            CustomerLabelHeaderId = entity.Id,
            UpdatedDate = entity.UpdatedDate
        }, "Updated customer label successfully.");
    }

    private static bool SetOptionalGuid(Guid? incoming, Func<Guid?> current, Action<Guid?> apply)
    {
        if (!incoming.HasValue || Nullable.Equals(incoming, current())) return false;
        apply(incoming);
        return true;
    }
}
