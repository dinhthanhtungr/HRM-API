using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
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

namespace HRM.Application.Features.CRM.Quotations.Commands.RefreshQuotationPrices
{
    internal sealed class RefreshQuotationPricesCommandHandler
       : IRequestHandler<RefreshQuotationPricesCommand, OperationResult<QuotationTotalsDto>>
    {
        private readonly ICRMReadDbContext _readDbContext;
        private readonly ICRMWriteDbContext _writeDbContext;
        private readonly ICustomerVisibilityService _visibilityService;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly KeyedMutationLock<Guid> _mutationLock;

        public RefreshQuotationPricesCommandHandler(
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
            RefreshQuotationPricesCommand command,
            CancellationToken cancellationToken)
        {
            var requestedLines = command.Request.Lines;
            if (command.QuotationId == Guid.Empty || requestedLines.Count == 0)
            {
                return OperationResult<QuotationTotalsDto>.Fail(
                    "QuotationId and at least one price line are required.");
            }

            if (requestedLines.Count > QuotationRules.MaximumLineCount)
            {
                return OperationResult<QuotationTotalsDto>.Fail(
                    $"A price refresh cannot contain more than {QuotationRules.MaximumLineCount} lines.");
            }

            if (requestedLines.Any(x => x.QuotationLineId == Guid.Empty) ||
                requestedLines.Any(x => x.ProductPricingVersionId == Guid.Empty) ||
                requestedLines.Select(x => x.QuotationLineId).Distinct().Count() != requestedLines.Count)
            {
                return OperationResult<QuotationTotalsDto>.Fail(
                    "Price lines must have unique valid QuotationLineId and ProductPricingVersionId values.");
            }

            using var mutationLease = await _mutationLock.AcquireAsync(
                command.QuotationId,
                cancellationToken);

            var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
            var quotation = await _writeDbContext.Quotations
                .AsTracking()
                .Include(x => x.Lines)
                .ThenInclude(x => x.PriceTiers)
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
                    "Only a draft quotation can have its prices refreshed.");
            }

            var concurrencyError = OptimisticConcurrencyHelper.ValidateExpectedUpdatedDate(
                command.Request.ExpectedUpdatedDate,
                quotation.UpdatedDate,
                "Quotation");
            if (concurrencyError is not null)
            {
                return OperationResult<QuotationTotalsDto>.Fail(concurrencyError);
            }

            var lineLookup = quotation.Lines.ToDictionary(x => x.QuotationLineId);
            if (requestedLines.Any(x => !lineLookup.ContainsKey(x.QuotationLineId)))
            {
                return OperationResult<QuotationTotalsDto>.Fail(
                    "One or more quotation lines do not belong to this quotation.");
            }

            var pricingVersionIds = requestedLines
                .Select(x => x.ProductPricingVersionId)
                .Distinct()
                .ToArray();
            var pricingVersions = await _readDbContext.ProductPricingVersions
                .AsNoTracking()
                .Include(x => x.PriceTiers)
                .Where(x =>
                    pricingVersionIds.Contains(x.ProductPricingVersionId) &&
                    x.CompanyId == scope.CompanyId &&
                    x.Currency == quotation.Currency &&
                    x.Status == ProductPricingStatus.Approved &&
                    x.IsActive)
                .ToDictionaryAsync(x => x.ProductPricingVersionId, cancellationToken);
            if (pricingVersions.Count != pricingVersionIds.Length || requestedLines.Any(request =>
                    !pricingVersions.TryGetValue(request.ProductPricingVersionId, out var pricingVersion) ||
                    pricingVersion.ProductId != lineLookup[request.QuotationLineId].ProductId))
            {
                return OperationResult<QuotationTotalsDto>.Fail(
                    "One or more product pricing versions were not found, not approved, outside the current company/currency, or belong to another product.");
            }

            var pricingByLineId = new Dictionary<Guid, QuotationLinePricing>(requestedLines.Count);
            for (var index = 0; index < requestedLines.Count; index++)
            {
                var request = requestedLines[index];
                var line = lineLookup[request.QuotationLineId];
                var pricingVersion = pricingVersions[request.ProductPricingVersionId];
                var pricingResult = QuotationPricingSnapshotFactory.Create(
                    line.QuotationLineId,
                    scope.CompanyId,
                    line.ProductId,
                    quotation.Currency,
                    line.Quantity,
                    line.PriceMode,
                    pricingVersion,
                    $"lines[{index}]");
                if (!pricingResult.Success || pricingResult.Data is null)
                {
                    return OperationResult<QuotationTotalsDto>.Fail(pricingResult.Message!);
                }

                pricingByLineId.Add(line.QuotationLineId, pricingResult.Data);
            }

            foreach (var request in requestedLines)
            {
                var line = lineLookup[request.QuotationLineId];
                var pricing = pricingByLineId[line.QuotationLineId];

                var existingPriceTiers = line.PriceTiers.ToList();
                _writeDbContext.QuotationLinePriceTiers.RemoveRange(existingPriceTiers);
                line.PriceTiers.Clear();
                foreach (var priceTier in pricing.PriceTiers)
                {
                    line.PriceTiers.Add(priceTier);
                }
                _writeDbContext.QuotationLinePriceTiers.AddRange(pricing.PriceTiers);

                line.UnitPrice = pricing.EffectiveUnitPrice;
                line.ProductPricingVersionId = request.ProductPricingVersionId;
                line.LineTotal = QuotationRules.CalculateLineTotal(
                    line.Quantity,
                    line.UnitPrice,
                    line.DiscountPercent);
            }

            QuotationRules.RecalculateTotals(quotation);
            quotation.UpdatedBy = scope.EmployeeId;
            quotation.UpdatedDate = _dateTimeProvider.Now;

            try
            {
                var affectedRows = await _writeDbContext.SaveChangesAsync(cancellationToken);
                if (affectedRows == 0)
                {
                    return OperationResult<QuotationTotalsDto>.Fail(
                        "Quotation price refresh was not persisted.");
                }
            }
            catch (DbUpdateConcurrencyException exception)
            {
                return OperationResult<QuotationTotalsDto>.Fail(
                    OptimisticConcurrencyHelper.CreateConflictMessage("Quotation", exception));
            }

            return OperationResult<QuotationTotalsDto>.Ok(new QuotationTotalsDto
            {
                SubTotal = quotation.SubTotal,
                DiscountAmount = quotation.DiscountAmount,
                TaxPercent = quotation.TaxPercent,
                TaxAmount = quotation.TaxAmount,
                TotalAmount = quotation.TotalAmount,
                UpdatedDate = quotation.UpdatedDate
            }, "Quotation prices refreshed successfully.");
        }
    }

}
