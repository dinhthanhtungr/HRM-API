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

namespace HRM.Application.Features.CRM.Quotations.Commands.UpdateQuotationCustomerPriceTiers;

internal sealed class UpdateQuotationCustomerPriceTiersCommandHandler
    : IRequestHandler<UpdateQuotationCustomerPriceTiersCommand,
        OperationResult<QuotationTotalsDto>>
{
    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly KeyedMutationLock<Guid> _mutationLock;

    public UpdateQuotationCustomerPriceTiersCommandHandler(
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
        UpdateQuotationCustomerPriceTiersCommand command,
        CancellationToken cancellationToken)
    {
        var requestedLines = command.Request.Lines ?? [];
        if (command.QuotationId == Guid.Empty ||
            !command.Request.ExpectedUpdatedDate.HasValue ||
            requestedLines.Count == 0 ||
            requestedLines.Count > QuotationRules.MaximumLineCount)
        {
            return OperationResult<QuotationTotalsDto>.Fail(
                "QuotationId, expectedUpdatedDate and at least one customer price line are required.");
        }

        if (requestedLines.Any(x => x.QuotationLineId == Guid.Empty) ||
            requestedLines.Select(x => x.QuotationLineId).Distinct().Count() != requestedLines.Count)
        {
            return OperationResult<QuotationTotalsDto>.Fail(
                "Quotation line ids must be valid and unique.");
        }

        using var lease = await _mutationLock.AcquireAsync(
            command.QuotationId, cancellationToken);
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var quotation = await _writeDbContext.Quotations
            .AsTracking()
            .Include(x => x.Lines.Where(line => line.IsActive))
                .ThenInclude(x => x.PriceTiers)
            .FirstOrDefaultAsync(x =>
                x.QuotationId == command.QuotationId &&
                x.CompanyId == scope.CompanyId &&
                x.IsActive,
                cancellationToken);
        if (quotation is null || !await _visibilityService
                .ApplyCustomerVisibility(_readDbContext.Customers.AsNoTracking(), scope)
                .AnyAsync(x => x.CustomerId == quotation.CustomerId, cancellationToken))
        {
            return OperationResult<QuotationTotalsDto>.Fail(
                "Quotation was not found or is outside your visibility scope.");
        }

        if (quotation.SaleEmployeeId != scope.EmployeeId)
        {
            return OperationResult<QuotationTotalsDto>.Fail(
                "Only the assigned sale employee can edit customer price tiers.");
        }

        if (!QuotationWorkflowRules.CanEditCustomerPricing(quotation.Status))
        {
            return OperationResult<QuotationTotalsDto>.Fail(
                "Customer price tiers can only be edited while pricing is pending or approved.");
        }

        var concurrencyError = OptimisticConcurrencyHelper.ValidateExpectedUpdatedDate(
            command.Request.ExpectedUpdatedDate, quotation.UpdatedDate, "Quotation");
        if (concurrencyError is not null)
        {
            return OperationResult<QuotationTotalsDto>.Fail(concurrencyError);
        }

        var lineById = quotation.Lines.ToDictionary(x => x.QuotationLineId);
        var pricingByLineId = new Dictionary<Guid, QuotationLinePricing>(requestedLines.Count);
        for (var index = 0; index < requestedLines.Count; index++)
        {
            var request = requestedLines[index];
            if (!lineById.TryGetValue(request.QuotationLineId, out var line))
            {
                return OperationResult<QuotationTotalsDto>.Fail(
                    $"lines[{index}].quotationLineId does not belong to this quotation.");
            }

            var result = QuotationPriceTierBuilder.Build(
                line.QuotationLineId,
                line.Quantity,
                request.PriceTiers,
                $"lines[{index}]");
            if (!result.Success || result.Data is null)
            {
                return OperationResult<QuotationTotalsDto>.Fail(result.Message!);
            }

            pricingByLineId.Add(line.QuotationLineId, result.Data);
        }

        foreach (var request in requestedLines)
        {
            var line = lineById[request.QuotationLineId];
            var pricing = pricingByLineId[line.QuotationLineId];
            _writeDbContext.QuotationLinePriceTiers.RemoveRange(line.PriceTiers);
            line.PriceTiers.Clear();
            foreach (var tier in pricing.PriceTiers)
            {
                line.PriceTiers.Add(tier);
            }

            _writeDbContext.QuotationLinePriceTiers.AddRange(pricing.PriceTiers);
            line.LineTotal = QuotationRules.CalculateLineTotal(
                line.Quantity, line.UnitPrice, line.DiscountPercent);
            line.Note = QuotationRules.TrimToNull(request.Note);
        }

        QuotationRules.RecalculateTotals(quotation);
        quotation.UpdatedBy = scope.EmployeeId;
        quotation.UpdatedDate = _dateTimeProvider.Now;

        try
        {
            await _writeDbContext.SaveChangesAsync(cancellationToken);
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
        }, "Customer price tiers updated successfully.");
    }
}
