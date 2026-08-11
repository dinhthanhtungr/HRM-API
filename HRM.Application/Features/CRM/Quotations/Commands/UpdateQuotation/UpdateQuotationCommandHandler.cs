using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.CRM.Quotations.Commands.UpdateQuotation
{
    internal sealed class UpdateQuotationCommandHandler
        : IRequestHandler<UpdateQuotationCommand, OperationResult<QuotationTotalsDto>>
    {
        private readonly ICRMReadDbContext _readDbContext;
        private readonly ICRMWriteDbContext _writeDbContext;
        private readonly ICustomerVisibilityService _visibilityService;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly KeyedMutationLock<Guid> _mutationLock;

        public UpdateQuotationCommandHandler(
            ICRMReadDbContext readDbContext,
            ICRMWriteDbContext writeDbContext,
            ICustomerVisibilityService visibilityService,
            IDateTimeProvider dateTimeProvider,
            KeyedMutationLock<Guid> mutationLock)
        {
            _readDbContext = readDbContext;
            _writeDbContext = writeDbContext;
            _visibilityService = visibilityService;
            _dateTimeProvider = dateTimeProvider;
            _mutationLock = mutationLock;
        }

        public async Task<OperationResult<QuotationTotalsDto>> Handle(
            UpdateQuotationCommand command,
            CancellationToken cancellationToken)
        {
            if (command.QuotationId == Guid.Empty)
            {
                return OperationResult<QuotationTotalsDto>.Fail("QuotationId is invalid.");
            }

            var request = command.Request;
            if (request.CustomerId == Guid.Empty)
            {
                return OperationResult<QuotationTotalsDto>.Fail("CustomerId is invalid.");
            }

            if (request.Currency is not null &&
                (string.IsNullOrWhiteSpace(request.Currency) ||
                 request.Currency.Trim().Length > QuotationRules.MaximumCurrencyLength))
            {
                return OperationResult<QuotationTotalsDto>.Fail(
                    $"Currency cannot be blank or exceed {QuotationRules.MaximumCurrencyLength} characters.");
            }

            if (request.ExchangeRate is <= 0m)
            {
                return OperationResult<QuotationTotalsDto>.Fail("ExchangeRate must be greater than zero.");
            }

            if ((request.QuotationDate.HasValue && request.QuotationDate.Value == default) ||
                (request.ValidUntil.HasValue && request.ValidUntil.Value == default))
            {
                return OperationResult<QuotationTotalsDto>.Fail(
                    "QuotationDate and ValidUntil must be valid dates when provided.");
            }

            using var mutationLease = await _mutationLock.AcquireAsync(
                command.QuotationId,
                cancellationToken);

            var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
            var quotation = await _writeDbContext.Quotations
                .AsTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.QuotationId == command.QuotationId &&
                        x.CompanyId == scope.CompanyId &&
                        x.IsActive,
                    cancellationToken);

            if (quotation is null)
            {
                return OperationResult<QuotationTotalsDto>.Fail(
                    "Quotation was not found or is outside your visibility scope.");
            }

            var canAccessCustomer = await _visibilityService
                .ApplyCustomerVisibility(_readDbContext.Customers.AsNoTracking(), scope)
                .AnyAsync(x => x.CustomerId == quotation.CustomerId, cancellationToken);
            if (!canAccessCustomer)
            {
                return OperationResult<QuotationTotalsDto>.Fail(
                    "Quotation was not found or is outside your visibility scope.");
            }

            if (quotation.Status != QuotationStatus.Draft)
            {
                return OperationResult<QuotationTotalsDto>.Fail(
                    "Only a draft quotation can be updated.");
            }

            var concurrencyError = OptimisticConcurrencyHelper.ValidateExpectedUpdatedDate(
                request.ExpectedUpdatedDate,
                quotation.UpdatedDate,
                "Quotation");
            if (concurrencyError is not null)
            {
                return OperationResult<QuotationTotalsDto>.Fail(concurrencyError);
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
                    return OperationResult<QuotationTotalsDto>.Fail(
                        "Customer was not found or is outside your visibility scope.");
                }

                var hasSampleRequestOutsideTargetCustomer = await _readDbContext.QuotationLines
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.QuotationId == quotation.QuotationId &&
                        x.SampleRequestId.HasValue &&
                        (x.SampleRequest!.CompanyId != scope.CompanyId ||
                         x.SampleRequest.CustomerId != targetCustomerId),
                        cancellationToken);
                if (hasSampleRequestOutsideTargetCustomer)
                {
                    return OperationResult<QuotationTotalsDto>.Fail(
                        "Customer cannot be changed while quotation lines reference sample requests from another customer.");
                }
            }

            var contactResolution = await ResolveContactAsync(
                targetCustomerId,
                request.ContactId,
                cancellationToken);
            if (!contactResolution.Success)
            {
                return OperationResult<QuotationTotalsDto>.Fail(contactResolution.Error!);
            }

            var targetQuotationDate = request.QuotationDate ?? quotation.QuotationDate;
            var targetValidUntil = request.ValidUntil ?? quotation.ValidUntil;
            if (targetValidUntil.HasValue && targetValidUntil.Value < targetQuotationDate)
            {
                return OperationResult<QuotationTotalsDto>.Fail(
                    "ValidUntil cannot be earlier than QuotationDate.");
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
            var taxPercentChanged = PatchHelper.SetIfHasValue(
                request.TaxPercent.HasValue
                    ? Math.Clamp(request.TaxPercent.Value, 0m, 100m)
                    : null,
                () => quotation.TaxPercent,
                value => quotation.TaxPercent = value);
            changed |= taxPercentChanged;
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

            if (taxPercentChanged)
            {
                QuotationRules.RecalculateHeaderTax(quotation);
            }

            if (changed)
            {
                quotation.UpdatedBy = scope.EmployeeId;
                quotation.UpdatedDate = _dateTimeProvider.Now;
                try
                {
                    var affectedRows = await _writeDbContext.SaveChangesAsync(cancellationToken);
                    if (affectedRows == 0)
                    {
                        return OperationResult<QuotationTotalsDto>.Fail(
                            "Quotation update was not persisted.");
                    }
                }
                catch (DbUpdateConcurrencyException exception)
                {
                    return OperationResult<QuotationTotalsDto>.Fail(
                        OptimisticConcurrencyHelper.CreateConflictMessage("Quotation", exception));
                }
            }

            return OperationResult<QuotationTotalsDto>.Ok(
                new QuotationTotalsDto
                {
                    SubTotal = quotation.SubTotal,
                    DiscountAmount = quotation.DiscountAmount,
                    TaxPercent = quotation.TaxPercent,
                    TaxAmount = quotation.TaxAmount,
                    TotalAmount = quotation.TotalAmount,
                    UpdatedDate = quotation.UpdatedDate
                },
                changed
                    ? "Quotation updated successfully."
                    : "Quotation already contains the requested values.");
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

}
