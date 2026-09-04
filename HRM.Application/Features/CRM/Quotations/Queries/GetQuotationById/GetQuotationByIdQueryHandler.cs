using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
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

namespace HRM.Application.Features.CRM.Quotations.Queries.GetQuotationById
{
    internal sealed class GetQuotationByIdQueryHandler
        : IRequestHandler<GetQuotationByIdQuery, OperationResult<QuotationDetailDto>>
    {
        private readonly ICRMReadDbContext _dbContext;
        private readonly ICustomerVisibilityService _visibilityService;
        private readonly QuotationTierPriceReferenceService _tierPriceReferenceService;

        public GetQuotationByIdQueryHandler(
            ICRMReadDbContext dbContext,
            ICustomerVisibilityService visibilityService,
            QuotationTierPriceReferenceService tierPriceReferenceService)
        {
            _dbContext = dbContext;
            _visibilityService = visibilityService;
            _tierPriceReferenceService = tierPriceReferenceService;
        }

        public async Task<OperationResult<QuotationDetailDto>> Handle(
            GetQuotationByIdQuery request,
            CancellationToken cancellationToken)
        {
            if (request.QuotationId == Guid.Empty)
            {
                return OperationResult<QuotationDetailDto>.Fail("QuotationId is invalid.");
            }

            var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
            var detail = await _visibilityService
                .ApplyQuotationVisibility(
                    _dbContext.Quotations.AsNoTracking(),
                    _dbContext.Customers.AsNoTracking(),
                    scope)
                .Where(x => x.QuotationId == request.QuotationId)
                .Select(x => new QuotationDetailDto
                {
                    QuotationId = x.QuotationId,
                    ExternalId = x.ExternalId,
                    CustomerId = x.CustomerId,
                    CustomerExternalId = x.Customer.ExternalId,
                    CustomerName = x.Customer.CustomerName,
                    ContactId = x.ContactId,
                    ContactName = x.ContactName,
                    ContactPhone = x.ContactPhone,
                    CustomerAddressSnapshot = x.CustomerAddressSnapshot,
                    SaleEmployeeId = x.SaleEmployeeId,
                    SaleEmployeeName = x.SaleEmployee.FullName,
                    Status = x.Status,
                    Currency = x.Currency,
                    ExchangeRate = x.ExchangeRate,
                    SubTotal = x.SubTotal,
                    DiscountAmount = x.DiscountAmount,
                    TaxPercent = x.TaxPercent,
                    TaxAmount = x.TaxAmount,
                    TotalAmount = x.TotalAmount,
                    QuotationDate = x.QuotationDate,
                    ValidUntil = x.ValidUntil,
                    SentDate = x.SentDate,
                    PaymentTerms = x.PaymentTerms,
                    DeliveryTerms = x.DeliveryTerms,
                    Note = x.Note,
                    Terms = x.Terms
                        .Where(term => term.IsActive)
                        .OrderBy(term => term.SortOrder)
                        .ThenBy(term => term.QuotationTermId)
                        .Select(term => new QuotationTermDto
                        {
                            QuotationTermId = term.QuotationTermId,
                            LabelVi = term.LabelVi,
                            LabelEn = term.LabelEn,
                            ValueVi = term.ValueVi,
                            ValueEn = term.ValueEn,
                            SortOrder = term.SortOrder,
                            IsActive = term.IsActive
                        })
                        .ToList(),
                    Version = x.Version,
                    CreatedDate = x.CreatedDate,
                    UpdatedDate = x.UpdatedDate,
                    Lines = x.Lines
                        .Where(line => line.IsActive)
                        .OrderBy(line => line.SortOrder)
                        .ThenBy(line => line.QuotationLineId)
                        .Select(line => new QuotationLineDto
                        {
                            QuotationLineId = line.QuotationLineId,
                            ProductId = line.ProductId,
                            SampleRequestId = line.SampleRequestId,
                            ProductPricingVersionId = line.ProductPricingVersionId,
                            ProductPricingVersion = line.ProductPricingVersion != null
                                ? line.ProductPricingVersion.Version
                                : null,
                            ProductPricingStatus = line.ProductPricingVersion != null
                                ? line.ProductPricingVersion.Status
                                : null,
                            HasApprovedPricingAvailable = _dbContext.ProductPricingVersions.Any(pricing =>
                                pricing.CompanyId == x.CompanyId &&
                                pricing.ProductId == line.ProductId &&
                                pricing.Currency == x.Currency &&
                                pricing.Status == ProductPricingStatus.Approved &&
                                pricing.IsActive),
                            HasNewerPricingVersion = line.ProductPricingVersion != null &&
                                _dbContext.ProductPricingVersions.Any(pricing =>
                                    pricing.CompanyId == x.CompanyId &&
                                    pricing.ProductId == line.ProductId &&
                                    pricing.Currency == x.Currency &&
                                    pricing.Status == ProductPricingStatus.Approved &&
                                    pricing.IsActive &&
                                    pricing.Version > line.ProductPricingVersion.Version),
                            ProductExternalId = line.ProductExternalIdSnapshot,
                            ProductName = line.ProductNameSnapshot,
                            Quantity = line.Quantity,
                            Unit = line.Unit,
                            PriceMode = line.PriceMode,
                            UnitPrice = line.UnitPrice,
                            DiscountPercent = line.DiscountPercent,
                            LineTotal = line.LineTotal,
                            PriceTiers = line.PriceTiers
                                .OrderBy(tier => tier.SortOrder)
                                .Select(tier => new QuotationLinePriceTierDto
                                {
                                    QuotationLinePriceTierId = tier.QuotationLinePriceTierId,
                                    IsSnapshot = true,
                                    IsActive = tier.IsActive,
                                    RequiresManualPrice = false,
                                    QuantityRangeLabel = tier.QuantityRangeLabel,
                                    MinQuantity = tier.MinQuantity,
                                    MaxQuantity = tier.MaxQuantity,
                                    MinInclusive = tier.MinInclusive,
                                    MaxInclusive = tier.MaxInclusive,
                                    UnitPrice = tier.UnitPrice,
                                    CommissionAmount = tier.CommissionAmount,
                                    CustomerUnitPrice = tier.CustomerUnitPrice,
                                    SortOrder = tier.SortOrder
                                })
                                .ToList(),
                            Note = line.Note,
                            SortOrder = line.SortOrder
                        })
                        .ToList(),
                    StatusHistories = x.StatusHistories
                        .OrderByDescending(history => history.ChangedDate)
                        .Select(history => new QuotationStatusHistoryDto
                        {
                            Id = history.Id,
                            FromStatus = history.FromStatus,
                            ToStatus = history.ToStatus,
                            Note = history.Note,
                            ChangedBy = history.ChangedBy,
                            ChangedByName = history.ChangedByNavigation.FullName,
                            ChangedDate = history.ChangedDate
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (detail is null)
            {
                return OperationResult<QuotationDetailDto>.Fail(
                    "Quotation was not found or is outside your visibility scope.");
            }

            var visibleQuotations = _visibilityService.ApplyQuotationVisibility(
                _dbContext.Quotations.AsNoTracking(),
                _dbContext.Customers.AsNoTracking(),
                scope);
            await _tierPriceReferenceService.EnrichAsync(
                detail,
                scope.CompanyId,
                visibleQuotations,
                cancellationToken);

            return OperationResult<QuotationDetailDto>.Ok(detail);
        }
    }

}
