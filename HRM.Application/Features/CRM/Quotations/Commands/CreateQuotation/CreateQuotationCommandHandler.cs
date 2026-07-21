using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.Category;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.CRM.Quotations.Commands.CreateQuotation
{
    internal sealed class CreateQuotationCommandHandler
        : IRequestHandler<CreateQuotationCommand, OperationResult<QuotationCreateResultDto>>
    {
        private readonly ICRMReadDbContext _readDbContext;
        private readonly ICRMWriteDbContext _writeDbContext;
        private readonly ICustomerVisibilityService _visibilityService;
        private readonly IExternalIdService _externalIdService;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly QuotationLineBuilder _lineBuilder;

        public CreateQuotationCommandHandler(
            ICRMReadDbContext readDbContext,
            ICRMWriteDbContext writeDbContext,
            ICustomerVisibilityService visibilityService,
            IExternalIdService externalIdService,
            IDateTimeProvider dateTimeProvider,
            QuotationLineBuilder lineBuilder)
        {
            _readDbContext = readDbContext;
            _writeDbContext = writeDbContext;
            _visibilityService = visibilityService;
            _externalIdService = externalIdService;
            _dateTimeProvider = dateTimeProvider;
            _lineBuilder = lineBuilder;
        }

        public async Task<OperationResult<QuotationCreateResultDto>> Handle(
            CreateQuotationCommand command,
            CancellationToken cancellationToken)
        {
            var request = command.Request;
            if (request.CustomerId == Guid.Empty)
            {
                return OperationResult<QuotationCreateResultDto>.Fail("CustomerId is required.");
            }

            var currency = QuotationRules.TrimToNull(request.Currency);
            if (currency is null || currency.Length > QuotationRules.MaximumCurrencyLength)
            {
                return OperationResult<QuotationCreateResultDto>.Fail(
                    $"Currency is required and cannot exceed {QuotationRules.MaximumCurrencyLength} characters.");
            }

            if (request.ExchangeRate <= 0m)
            {
                return OperationResult<QuotationCreateResultDto>.Fail("ExchangeRate must be greater than zero.");
            }

            if ((request.QuotationDate.HasValue && request.QuotationDate.Value == default) ||
                (request.ValidUntil.HasValue && request.ValidUntil.Value == default))
            {
                return OperationResult<QuotationCreateResultDto>.Fail(
                    "QuotationDate and ValidUntil must be valid dates when provided.");
            }

            var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
            var customerExists = await _visibilityService
                .ApplyCustomerVisibility(_readDbContext.Customers.AsNoTracking(), scope)
                .AnyAsync(x => x.CustomerId == request.CustomerId, cancellationToken);

            if (!customerExists)
            {
                return OperationResult<QuotationCreateResultDto>.Fail(
                    "Customer was not found or is outside your visibility scope.");
            }

            var contactResult = await ResolveContactAsync(
                request.CustomerId,
                request.ContactId,
                request.ContactName,
                cancellationToken);
            if (!contactResult.Success)
            {
                return OperationResult<QuotationCreateResultDto>.Fail(contactResult.Error!);
            }

            var now = _dateTimeProvider.Now;
            var quotationDate = request.QuotationDate ?? now;
            if (request.ValidUntil.HasValue && request.ValidUntil.Value < quotationDate)
            {
                return OperationResult<QuotationCreateResultDto>.Fail(
                    "ValidUntil cannot be earlier than QuotationDate.");
            }

            var externalId = string.IsNullOrWhiteSpace(request.ExternalId)
                ? await _externalIdService.GenerateMonthlyCodeAsync(
                    scope.CompanyId,
                    DocumentPrefix.BG.ToString(),
                    cancellationToken)
                : request.ExternalId.Trim();

            var externalIdExists = await _readDbContext.Quotations
                .AsNoTracking()
                .AnyAsync(x =>
                    x.CompanyId == scope.CompanyId &&
                    x.ExternalId == externalId &&
                    x.Version == 1,
                    cancellationToken);
            if (externalIdExists)
            {
                return OperationResult<QuotationCreateResultDto>.Fail(
                    $"Quotation external id {externalId} already exists in this company.");
            }

            var quotationId = Guid.CreateVersion7();
            var lineResult = await _lineBuilder.BuildAsync(
                quotationId,
                scope.CompanyId,
                request.Lines,
                cancellationToken);
            if (!lineResult.Success || lineResult.Data is null)
            {
                return OperationResult<QuotationCreateResultDto>.Fail(lineResult.Message!);
            }

            var quotation = new Quotation
            {
                QuotationId = quotationId,
                ExternalId = externalId,
                CustomerId = request.CustomerId,
                ContactId = request.ContactId,
                ContactName = contactResult.ContactName,
                CompanyId = scope.CompanyId,
                SaleEmployeeId = scope.EmployeeId,
                Status = QuotationStatus.Draft,
                Currency = currency.ToUpperInvariant(),
                ExchangeRate = request.ExchangeRate,
                QuotationDate = quotationDate,
                ValidUntil = request.ValidUntil,
                PaymentTerms = QuotationRules.TrimToNull(request.PaymentTerms),
                DeliveryTerms = QuotationRules.TrimToNull(request.DeliveryTerms),
                Note = QuotationRules.TrimToNull(request.Note),
                Version = 1,
                IsActive = true,
                CreatedBy = scope.EmployeeId,
                CreatedDate = now,
                UpdatedBy = scope.EmployeeId,
                UpdatedDate = now,
                Lines = lineResult.Data.ToList()
            };
            QuotationRules.RecalculateTotals(quotation);

            await _writeDbContext.Quotations.AddAsync(quotation, cancellationToken);
            await _writeDbContext.SaveChangesAsync(cancellationToken);

            return OperationResult<QuotationCreateResultDto>.Ok(new QuotationCreateResultDto
            {
                QuotationId = quotationId,
                ExternalId = externalId
            }, "Quotation draft created successfully.");
        }

        private async Task<ContactResolution> ResolveContactAsync(
            Guid customerId,
            Guid? contactId,
            string? requestedContactName,
            CancellationToken cancellationToken)
        {
            if (!contactId.HasValue)
            {
                return ContactResolution.Allowed(QuotationRules.TrimToNull(requestedContactName));
            }

            if (contactId.Value == Guid.Empty)
            {
                return ContactResolution.Denied("ContactId is invalid.");
            }

            var contact = await _readDbContext.Contacts
                .AsNoTracking()
                .Where(x =>
                    x.ContactId == contactId.Value &&
                    x.CustomerId == customerId &&
                    x.IsActive)
                .Select(x => new { x.FirstName, x.LastName })
                .FirstOrDefaultAsync(cancellationToken);

            if (contact is null)
            {
                return ContactResolution.Denied(
                    "Contact was not found or does not belong to the selected customer.");
            }

            var snapshot = QuotationRules.TrimToNull(requestedContactName)
                ?? QuotationRules.TrimToNull($"{contact.FirstName} {contact.LastName}");
            return ContactResolution.Allowed(snapshot);
        }

        private sealed record ContactResolution(bool Success, string? ContactName, string? Error)
        {
            public static ContactResolution Allowed(string? contactName) => new(true, contactName, null);
            public static ContactResolution Denied(string error) => new(false, null, error);
        }
    }

}
