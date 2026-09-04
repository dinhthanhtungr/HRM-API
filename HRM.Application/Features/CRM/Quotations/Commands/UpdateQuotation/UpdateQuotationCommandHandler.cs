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

            IReadOnlyList<HRM.Domain.Entities.CustomerSchema.QuotationTerm>? replacementTerms = null;
            if (request.Terms is not null)
            {
                var termResult = QuotationTermBuilder.Build(
                    command.QuotationId,
                    request.Terms);
                if (!termResult.Success || termResult.Data is null)
                {
                    return OperationResult<QuotationTotalsDto>.Fail(termResult.Message!);
                }

                replacementTerms = termResult.Data;
            }

            using var mutationLease = await _mutationLock.AcquireAsync(
                command.QuotationId,
                cancellationToken);

            var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
            var quotation = await _writeDbContext.Quotations
                .AsTracking()
                .Include(x => x.Terms)
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

            if (quotation.Status is not (
                QuotationStatus.Draft or
                QuotationStatus.PendingApproval or
                QuotationStatus.Approved))
            {
                return OperationResult<QuotationTotalsDto>.Fail(
                    "Only a draft, pending or approved quotation can be updated.");
            }

            if (QuotationRules.TrimToNull(request.ContactPhone)?.Length >
                QuotationRules.MaximumContactPhoneLength)
            {
                return OperationResult<QuotationTotalsDto>.Fail(
                    $"ContactPhone cannot exceed {QuotationRules.MaximumContactPhoneLength} characters.");
            }

            if (QuotationRules.TrimToNull(request.CustomerAddressSnapshot)?.Length >
                QuotationRules.MaximumCustomerAddressLength)
            {
                return OperationResult<QuotationTotalsDto>.Fail(
                    $"CustomerAddressSnapshot cannot exceed {QuotationRules.MaximumCustomerAddressLength} characters.");
            }

            if (quotation.Status is QuotationStatus.PendingApproval or QuotationStatus.Approved)
            {
                if (request.CustomerId.HasValue ||
                    request.Currency is not null ||
                    request.ExchangeRate.HasValue ||
                    request.QuotationDate.HasValue)
                {
                    return OperationResult<QuotationTotalsDto>.Fail(
                        "Customer, currency, exchange rate and quotation date cannot be changed after pricing approval. Withdraw the pricing request first.");
                }
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
            string? changedCustomerAddress = null;
            if (customerChanged)
            {
                var targetCustomer = await _visibilityService
                    .ApplyCustomerVisibility(_readDbContext.Customers.AsNoTracking(), scope)
                    .Where(x => x.CustomerId == targetCustomerId)
                    .Select(x => new { x.CustomerId, x.RegistrationAddress })
                    .FirstOrDefaultAsync(cancellationToken);
                if (targetCustomer is null)
                {
                    return OperationResult<QuotationTotalsDto>.Fail(
                        "Customer was not found or is outside your visibility scope.");
                }
                changedCustomerAddress = QuotationRules.TrimToNull(
                    targetCustomer.RegistrationAddress);

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

            var targetCurrency = QuotationRules.TrimToNull(request.Currency)?.ToUpperInvariant();
            if (targetCurrency is not null && targetCurrency != quotation.Currency)
            {
                var hasPricedLines = await _readDbContext.QuotationLines
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.QuotationId == quotation.QuotationId &&
                        (x.ProductPricingVersionId.HasValue || x.PriceTiers.Any()),
                        cancellationToken);
                if (hasPricedLines)
                {
                    return OperationResult<QuotationTotalsDto>.Fail(
                        "Currency cannot be changed while quotation lines contain pricing snapshots. Create or revise the quotation in the target currency instead.");
                }
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

            if (customerChanged && request.CustomerAddressSnapshot is null)
            {
                changed |= PatchHelper.SetNullableRef(
                    changedCustomerAddress,
                    () => quotation.CustomerAddressSnapshot,
                    value => quotation.CustomerAddressSnapshot = value);
            }

            changed |= PatchHelper.SetTrimmed(
                request.CustomerAddressSnapshot,
                () => quotation.CustomerAddressSnapshot,
                value => quotation.CustomerAddressSnapshot = value);

            if (customerChanged && !request.ContactId.HasValue)
            {
                quotation.ContactId = null;
                quotation.ContactName = null;
                quotation.ContactPhone = null;
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

                if (request.ContactPhone is null)
                {
                    changed |= PatchHelper.SetNullableRef(
                        contactResolution.ContactPhone,
                        () => quotation.ContactPhone,
                        value => quotation.ContactPhone = value);
                }
            }

            changed |= PatchHelper.SetTrimmed(
                request.ContactName,
                () => quotation.ContactName,
                value => quotation.ContactName = value);
            changed |= PatchHelper.SetTrimmed(
                request.ContactPhone,
                () => quotation.ContactPhone,
                value => quotation.ContactPhone = value);

            if (replacementTerms is not null)
            {
                foreach (var existingTerm in quotation.Terms.Where(x => x.IsActive))
                {
                    existingTerm.IsActive = false;
                }

                foreach (var term in replacementTerms)
                {
                    quotation.Terms.Add(term);
                }

                _writeDbContext.QuotationTerms.AddRange(replacementTerms);
                changed = true;
            }

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
                return ContactResolution.Allowed(null, null);
            }

            var contact = await _readDbContext.Contacts
                .AsNoTracking()
                .Where(x =>
                    x.ContactId == contactId.Value &&
                    x.CustomerId == customerId &&
                    x.IsActive)
                .Select(x => new { x.FirstName, x.LastName, x.Phone })
                .FirstOrDefaultAsync(cancellationToken);

            return contact is null
                ? ContactResolution.Denied(
                    "Contact was not found or does not belong to the selected customer.")
                : ContactResolution.Allowed(
                    QuotationRules.TrimToNull($"{contact.FirstName} {contact.LastName}"),
                    QuotationRules.TrimToNull(contact.Phone));
        }

        private sealed record ContactResolution(
            bool Success,
            string? ContactName,
            string? ContactPhone,
            string? Error)
        {
            public static ContactResolution Allowed(string? contactName, string? contactPhone)
                => new(true, contactName, contactPhone, null);
            public static ContactResolution Denied(string error)
                => new(false, null, null, error);
        }
    }

}
