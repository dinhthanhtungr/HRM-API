using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Commands.RefreshQuotationPrices;

/// <summary>
/// Ghi giá mới đã xin lại cho các dòng được chọn và tính lại tổng báo giá nháp.
/// </summary>
public sealed record RefreshQuotationPricesCommand(Guid QuotationId, RefreshQuotationPricesRequest Request)
    : IRequest<OperationResult<QuotationTotalsDto>>;

internal sealed class RefreshQuotationPricesCommandHandler
    : IRequestHandler<RefreshQuotationPricesCommand, OperationResult<QuotationTotalsDto>>
{
    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RefreshQuotationPricesCommandHandler(
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

        if (requestedLines.Any(x => x.QuotationLineId == Guid.Empty || x.UnitPrice < 0m) ||
            requestedLines.Select(x => x.QuotationLineId).Distinct().Count() != requestedLines.Count)
        {
            return OperationResult<QuotationTotalsDto>.Fail(
                "Price lines must have unique valid QuotationLineId values and non-negative UnitPrice values.");
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var quotation = await _visibilityService
            .ApplyQuotationVisibility(
                _writeDbContext.Quotations.Include(x => x.Lines),
                _readDbContext.Customers.AsNoTracking(),
                scope)
            .FirstOrDefaultAsync(x => x.QuotationId == command.QuotationId, cancellationToken);

        if (quotation is null)
        {
            return OperationResult<QuotationTotalsDto>.Fail(
                "Quotation was not found or is outside your visibility scope.");
        }

        if (quotation.Status != QuotationStatus.Draft)
        {
            return OperationResult<QuotationTotalsDto>.Fail(
                "Only a draft quotation can have its prices refreshed.");
        }

        var lineLookup = quotation.Lines.ToDictionary(x => x.QuotationLineId);
        if (requestedLines.Any(x => !lineLookup.ContainsKey(x.QuotationLineId)))
        {
            return OperationResult<QuotationTotalsDto>.Fail(
                "One or more quotation lines do not belong to this quotation.");
        }

        foreach (var request in requestedLines)
        {
            var line = lineLookup[request.QuotationLineId];
            line.UnitPrice = request.UnitPrice;
            line.LineTotal = QuotationRules.CalculateLineTotal(
                line.Quantity,
                line.UnitPrice,
                line.DiscountPercent,
                line.TaxPercent);
        }

        QuotationRules.RecalculateTotals(quotation);
        quotation.UpdatedBy = scope.EmployeeId;
        quotation.UpdatedDate = _dateTimeProvider.Now;

        await _writeDbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<QuotationTotalsDto>.Ok(new QuotationTotalsDto
        {
            SubTotal = quotation.SubTotal,
            DiscountAmount = quotation.DiscountAmount,
            TaxAmount = quotation.TaxAmount,
            TotalAmount = quotation.TotalAmount
        }, "Quotation prices refreshed successfully.");
    }
}
