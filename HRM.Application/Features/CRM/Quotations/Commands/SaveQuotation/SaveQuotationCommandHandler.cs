using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Commands.SaveQuotation;

internal sealed class SaveQuotationCommandHandler(
    ICRMReadDbContext readDbContext,
    ICRMWriteDbContext writeDbContext,
    ICustomerVisibilityService visibilityService,
    QuotationHeaderUpdateService headerService,
    QuotationLinesReplaceService linesService,
    QuotationCustomerPriceUpdateService customerPriceService,
    QuotationAtomicSaveService atomicSaveService,
    KeyedMutationLock<Guid> mutationLock,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<SaveQuotationCommand, OperationResult<QuotationTotalsDto>>
{
    public async Task<OperationResult<QuotationTotalsDto>> Handle(
        SaveQuotationCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var validationError = QuotationSaveRules.Validate(request);
        if (command.QuotationId == Guid.Empty || validationError is not null)
            return OperationResult<QuotationTotalsDto>.Fail(validationError ?? "QuotationId is invalid.");

        using var lease = await mutationLock.AcquireAsync(command.QuotationId, cancellationToken);
        return await atomicSaveService.ExecuteAsync(async () =>
        {
            var scope = await visibilityService.BuildScopeAsync(cancellationToken);
            var quotation = await writeDbContext.Quotations.AsTracking()
                .FirstOrDefaultAsync(x => x.QuotationId == command.QuotationId &&
                    x.CompanyId == scope.CompanyId && x.IsActive, cancellationToken);
            if (quotation is null || !await visibilityService
                .ApplyCustomerVisibility(readDbContext.Customers.AsNoTracking(), scope)
                .AnyAsync(x => x.CustomerId == quotation.CustomerId, cancellationToken))
                return OperationResult<QuotationTotalsDto>.Fail(
                    "Quotation was not found or is outside your visibility scope.");

            var statusError = QuotationSaveRules.ValidateStatus(quotation, request);
            var conflict = OptimisticConcurrencyHelper.ValidateExpectedUpdatedDate(
                request.ExpectedUpdatedDate, quotation.UpdatedDate, "Quotation");
            if (statusError is not null || conflict is not null)
                return OperationResult<QuotationTotalsDto>.Fail(statusError ?? conflict!);

            // Clear before validating the resulting dates and again after contact defaults are resolved.
            QuotationSaveRules.Clear(quotation, request.ClearFields);
            // Shared services stage tracked changes only; no section can independently persist.
            var headerResult = await headerService.ApplyAsync(
                command.QuotationId, request.Header ?? new UpdateQuotationRequest(),
                request.ExpectedUpdatedDate, persist: false, cancellationToken);
            if (!headerResult.Success) return headerResult;
            QuotationSaveRules.Clear(quotation, request.ClearFields);

            if (request.Lines is not null)
            {
                var result = await linesService.ApplyAsync(command.QuotationId,
                    new ReplaceQuotationLinesRequest { Lines = request.Lines },
                    quotation.UpdatedDate, persist: false, cancellationToken);
                if (!result.Success) return result;
            }
            if (request.CustomerPriceLines is not null)
            {
                var result = await customerPriceService.ApplyAsync(command.QuotationId,
                    new UpdateQuotationCustomerPriceTiersRequest { Lines = request.CustomerPriceLines },
                    quotation.UpdatedDate, persist: false, cancellationToken);
                if (!result.Success) return result;
            }

            quotation.UpdatedBy = scope.EmployeeId;
            var now = dateTimeProvider.Now;
            // Match PostgreSQL microsecond precision so the returned token round-trips unchanged.
            quotation.UpdatedDate = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerMicrosecond, now.Kind);
            return OperationResult<QuotationTotalsDto>.Ok(new QuotationTotalsDto
            {
                SubTotal = quotation.SubTotal,
                DiscountAmount = quotation.DiscountAmount,
                TaxPercent = quotation.TaxPercent,
                TaxAmount = quotation.TaxAmount,
                TotalAmount = quotation.TotalAmount,
                UpdatedDate = quotation.UpdatedDate
            }, "Quotation saved successfully.");
        }, cancellationToken);
    }
}
