using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Commands.UpdateQuotation;

/// <summary>
/// Cập nhật phần thông tin chung của báo giá nháp; dòng hàng được quản lý qua endpoint riêng.
/// </summary>
public sealed record UpdateQuotationCommand(Guid QuotationId, UpdateQuotationRequest Request)
    : IRequest<OperationResult>;

internal sealed class UpdateQuotationCommandHandler
    : IRequestHandler<UpdateQuotationCommand, OperationResult>
{
    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdateQuotationCommandHandler(
        ICRMReadDbContext readDbContext,
        ICRMWriteDbContext writeDbContext,
        ICustomerVisibilityService visibilityService,
        IDateTimeProvider dateTimeProvider)
    {
        _readDbContext = readDbContext;
        _writeDbContext = writeDbContext;
        _visibilityService = visibilityService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult> Handle(
        UpdateQuotationCommand command,
        CancellationToken cancellationToken)
    {
        if (command.QuotationId == Guid.Empty)
        {
            return OperationResult.Fail("QuotationId is invalid.");
        }

        var request = command.Request;
        if (request.CustomerId == Guid.Empty)
        {
            return OperationResult.Fail("CustomerId is invalid.");
        }

        if (request.Currency is not null &&
            (string.IsNullOrWhiteSpace(request.Currency) ||
             request.Currency.Trim().Length > QuotationRules.MaximumCurrencyLength))
        {
            return OperationResult.Fail(
                $"Currency cannot be blank or exceed {QuotationRules.MaximumCurrencyLength} characters.");
        }

        if (request.ExchangeRate is <= 0m)
        {
            return OperationResult.Fail("ExchangeRate must be greater than zero.");
        }

        if ((request.QuotationDate.HasValue && request.QuotationDate.Value == default) ||
            (request.ValidUntil.HasValue && request.ValidUntil.Value == default))
        {
            return OperationResult.Fail(
                "QuotationDate and ValidUntil must be valid dates when provided.");
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var quotation = await _visibilityService
            .ApplyQuotationVisibility(
                _writeDbContext.Quotations,
                _readDbContext.Customers.AsNoTracking(),
                scope)
            .FirstOrDefaultAsync(x => x.QuotationId == command.QuotationId, cancellationToken);

        if (quotation is null)
        {
            return OperationResult.Fail("Quotation was not found or is outside your visibility scope.");
        }

        if (quotation.Status != QuotationStatus.Draft)
        {
            return OperationResult.Fail("Only a draft quotation can be updated.");
        }

        var targetCustomerId = request.CustomerId ?? quotation.CustomerId;
        var customerChanged = request.CustomerId.HasValue && request.CustomerId.Value != quotation.CustomerId;
        if (customerChanged)
        {
            var customerExists = await _visibilityService
                .ApplyCustomerVisibility(_readDbContext.Customers.AsNoTracking(), scope)
                .AnyAsync(x => x.CustomerId == targetCustomerId, cancellationToken);
            if (!customerExists)
            {
                return OperationResult.Fail(
                    "Customer was not found or is outside your visibility scope.");
            }
        }

        var contactResolution = await ResolveContactAsync(
            targetCustomerId,
            request.ContactId,
            cancellationToken);
        if (!contactResolution.Success)
        {
            return OperationResult.Fail(contactResolution.Error!);
        }

        var targetQuotationDate = request.QuotationDate ?? quotation.QuotationDate;
        var targetValidUntil = request.ValidUntil ?? quotation.ValidUntil;
        if (targetValidUntil.HasValue && targetValidUntil.Value < targetQuotationDate)
        {
            return OperationResult.Fail("ValidUntil cannot be earlier than QuotationDate.");
        }

        var changed = false;
        changed |= PatchHelper.SetGuidIfValid(
            request.CustomerId,
            () => quotation.CustomerId,
            value => quotation.CustomerId = value);
        changed |= PatchHelper.SetTrimmed(
            request.Currency,
            () => quotation.Currency,
            value => quotation.Currency = value!.ToUpperInvariant());
        changed |= PatchHelper.SetIfHasValue(
            request.ExchangeRate,
            () => quotation.ExchangeRate,
            value => quotation.ExchangeRate = value);
        changed |= PatchHelper.SetIfHasValue(
            request.QuotationDate,
            () => quotation.QuotationDate,
            value => quotation.QuotationDate = value);
        changed |= PatchHelper.SetIfHasValue(
            request.ValidUntil,
            () => quotation.ValidUntil ?? default,
            value => quotation.ValidUntil = value);
        changed |= PatchHelper.SetTrimmed(
            request.PaymentTerms,
            () => quotation.PaymentTerms,
            value => quotation.PaymentTerms = value);
        changed |= PatchHelper.SetTrimmed(
            request.DeliveryTerms,
            () => quotation.DeliveryTerms,
            value => quotation.DeliveryTerms = value);
        changed |= PatchHelper.SetTrimmed(
            request.Note,
            () => quotation.Note,
            value => quotation.Note = value);

        if (customerChanged && !request.ContactId.HasValue)
        {
            quotation.ContactId = null;
            quotation.ContactName = null;
            changed = true;
        }

        if (request.ContactId.HasValue)
        {
            var targetContactId = request.ContactId.Value == Guid.Empty
                ? (Guid?)null
                : request.ContactId.Value;
            changed |= PatchHelper.SetNullable(
                targetContactId,
                () => quotation.ContactId,
                value => quotation.ContactId = value);

            if (request.ContactName is null)
            {
                changed |= PatchHelper.SetNullableRef(
                    contactResolution.ContactName,
                    () => quotation.ContactName,
                    value => quotation.ContactName = value);
            }
        }

        changed |= PatchHelper.SetTrimmed(
            request.ContactName,
            () => quotation.ContactName,
            value => quotation.ContactName = value);

        if (changed)
        {
            quotation.UpdatedBy = scope.EmployeeId;
            quotation.UpdatedDate = _dateTimeProvider.Now;
            await _writeDbContext.SaveChangesAsync(cancellationToken);
        }

        return OperationResult.Ok("Quotation updated successfully.");
    }

    private async Task<ContactResolution> ResolveContactAsync(
        Guid customerId,
        Guid? contactId,
        CancellationToken cancellationToken)
    {
        if (!contactId.HasValue || contactId.Value == Guid.Empty)
        {
            return ContactResolution.Allowed(null);
        }

        var contact = await _readDbContext.Contacts
            .AsNoTracking()
            .Where(x =>
                x.ContactId == contactId.Value &&
                x.CustomerId == customerId &&
                x.IsActive)
            .Select(x => new { x.FirstName, x.LastName })
            .FirstOrDefaultAsync(cancellationToken);

        return contact is null
            ? ContactResolution.Denied(
                "Contact was not found or does not belong to the selected customer.")
            : ContactResolution.Allowed(
                QuotationRules.TrimToNull($"{contact.FirstName} {contact.LastName}"));
    }

    private sealed record ContactResolution(bool Success, string? ContactName, string? Error)
    {
        public static ContactResolution Allowed(string? contactName) => new(true, contactName, null);
        public static ContactResolution Denied(string error) => new(false, null, error);
    }
}
