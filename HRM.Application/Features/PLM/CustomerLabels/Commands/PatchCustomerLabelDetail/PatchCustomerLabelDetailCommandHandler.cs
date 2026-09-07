using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.PLM.CustomerLabels.Dtos;
using HRM.Application.Features.PLM.CustomerLabels.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.CustomerLabels.Commands.PatchCustomerLabelDetail;

internal sealed class PatchCustomerLabelDetailCommandHandler : IRequestHandler<PatchCustomerLabelDetailCommand, OperationResult<SaveCustomerLabelDetailResultDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public PatchCustomerLabelDetailCommandHandler(IPLMWriteDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<SaveCustomerLabelDetailResultDto>> Handle(PatchCustomerLabelDetailCommand request, CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId.GetValueOrDefault();
        if (request.CustomerLabelHeaderId == Guid.Empty || request.CustomerLabelDetailId == Guid.Empty || companyId == Guid.Empty)
            return OperationResult<SaveCustomerLabelDetailResultDto>.Fail("Customer label header, detail, or current company is invalid.");
        if (request.ClearFieldValue && request.FieldValue is not null)
            return OperationResult<SaveCustomerLabelDetailResultDto>.Fail("FieldValue cannot be both updated and cleared.");
        var validationError = CustomerLabelRules.ValidateOptionalText(request.FieldKey, "FieldKey", CustomerLabelRules.MaxShortTextLength, true)
            ?? CustomerLabelRules.ValidateOptionalText(request.FieldValue, "FieldValue", CustomerLabelRules.MaxFieldValueLength, true);
        if (validationError is not null) return OperationResult<SaveCustomerLabelDetailResultDto>.Fail(validationError);

        var entity = await _dbContext.CustomerLabelDetails.FirstOrDefaultAsync(detail =>
            detail.Id == request.CustomerLabelDetailId && detail.CustomerLabelHeaderId == request.CustomerLabelHeaderId &&
            detail.Header.Product.CompanyId == companyId && detail.Header.Customer.CompanyId == companyId, cancellationToken);
        if (entity is null) return OperationResult<SaveCustomerLabelDetailResultDto>.Fail("Customer label detail was not found or is outside the current company.");

        if (request.FieldKey is not null)
        {
            var fieldKey = request.FieldKey.Trim();
            var duplicate = await _dbContext.CustomerLabelDetails.AsNoTracking().AnyAsync(detail =>
                detail.CustomerLabelHeaderId == request.CustomerLabelHeaderId && detail.Id != entity.Id && detail.FieldKey == fieldKey, cancellationToken);
            if (duplicate) return OperationResult<SaveCustomerLabelDetailResultDto>.Fail("A detail with this FieldKey already exists in the customer label.");
        }

        var changed = PatchHelper.SetIfHasValue(request.LineNo, () => entity.LineNo, value => entity.LineNo = value)
            | PatchHelper.SetTrimmed(request.FieldKey, () => entity.FieldKey, value => entity.FieldKey = value!)
            | PatchHelper.SetTrimmed(request.FieldValue, () => entity.FieldValue, value => entity.FieldValue = value)
            | PatchHelper.SetIfHasValue(request.IsActive, () => entity.IsActive, value => entity.IsActive = value);
        if (request.ClearFieldValue)
            changed |= PatchHelper.SetNullableRef<string>(null, () => entity.FieldValue, value => entity.FieldValue = value);
        if (changed) await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<SaveCustomerLabelDetailResultDto>.Ok(new SaveCustomerLabelDetailResultDto
        { CustomerLabelDetailId = entity.Id, CustomerLabelHeaderId = entity.CustomerLabelHeaderId }, "Updated customer label detail successfully.");
    }
}
