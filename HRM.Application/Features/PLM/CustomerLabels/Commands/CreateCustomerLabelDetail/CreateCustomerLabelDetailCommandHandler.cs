using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.CustomerLabels.Dtos;
using HRM.Application.Features.PLM.CustomerLabels.Services;
using HRM.Domain.Entities.PrintectSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.CustomerLabels.Commands.CreateCustomerLabelDetail;

internal sealed class CreateCustomerLabelDetailCommandHandler : IRequestHandler<CreateCustomerLabelDetailCommand, OperationResult<SaveCustomerLabelDetailResultDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public CreateCustomerLabelDetailCommandHandler(IPLMWriteDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<SaveCustomerLabelDetailResultDto>> Handle(CreateCustomerLabelDetailCommand request, CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId.GetValueOrDefault();
        if (request.CustomerLabelHeaderId == Guid.Empty || companyId == Guid.Empty)
            return OperationResult<SaveCustomerLabelDetailResultDto>.Fail("CustomerLabelHeaderId or current company is invalid.");
        if (string.IsNullOrWhiteSpace(request.FieldKey))
            return OperationResult<SaveCustomerLabelDetailResultDto>.Fail("FieldKey must not be blank.");
        var validationError = CustomerLabelRules.ValidateOptionalText(request.FieldKey, "FieldKey", CustomerLabelRules.MaxShortTextLength, true)
            ?? CustomerLabelRules.ValidateOptionalText(request.FieldValue, "FieldValue", CustomerLabelRules.MaxFieldValueLength, false);
        if (validationError is not null) return OperationResult<SaveCustomerLabelDetailResultDto>.Fail(validationError);

        var headerExists = await _dbContext.CustomerLabelHeaders.AsNoTracking().AnyAsync(header =>
            header.Id == request.CustomerLabelHeaderId && header.Product.CompanyId == companyId && header.Customer.CompanyId == companyId,
            cancellationToken);
        if (!headerExists) return OperationResult<SaveCustomerLabelDetailResultDto>.Fail("Customer label was not found or is outside the current company.");

        var normalizedFieldKey = request.FieldKey.Trim();
        var exists = await _dbContext.CustomerLabelDetails.AsNoTracking().AnyAsync(detail =>
            detail.CustomerLabelHeaderId == request.CustomerLabelHeaderId && detail.FieldKey == normalizedFieldKey, cancellationToken);
        if (exists) return OperationResult<SaveCustomerLabelDetailResultDto>.Fail("A detail with this FieldKey already exists in the customer label.");

        var entity = new CustomerLabelDetail
        {
            Id = Guid.CreateVersion7(), CustomerLabelHeaderId = request.CustomerLabelHeaderId, LineNo = request.LineNo,
            FieldKey = normalizedFieldKey, FieldValue = CustomerLabelRules.Normalize(request.FieldValue), IsActive = request.IsActive
        };
        await _dbContext.CustomerLabelDetails.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<SaveCustomerLabelDetailResultDto>.Ok(new SaveCustomerLabelDetailResultDto
        { CustomerLabelDetailId = entity.Id, CustomerLabelHeaderId = entity.CustomerLabelHeaderId }, "Created customer label detail successfully.");
    }
}
