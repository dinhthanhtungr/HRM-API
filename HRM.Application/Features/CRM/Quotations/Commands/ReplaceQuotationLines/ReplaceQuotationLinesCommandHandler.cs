using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace HRM.Application.Features.CRM.Quotations.Commands.ReplaceQuotationLines
{
    internal sealed class ReplaceQuotationLinesCommandHandler
        : IRequestHandler<ReplaceQuotationLinesCommand, OperationResult<QuotationTotalsDto>>
    {
        private const int MaximumSaveAttempts = 2;

        private readonly ICRMReadDbContext _readDbContext;
        private readonly ICRMWriteDbContext _writeDbContext;
        private readonly ICustomerVisibilityService _visibilityService;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly QuotationLineBuilder _lineBuilder;
        private readonly QuotationConversationSubjectService _conversationSubjectService;
        private readonly KeyedMutationLock<Guid> _mutationLock;

        public ReplaceQuotationLinesCommandHandler(
            ICRMReadDbContext readDbContext,
            ICRMWriteDbContext writeDbContext,
            ICustomerVisibilityService visibilityService,
            IDateTimeProvider dateTimeProvider,
            QuotationLineBuilder lineBuilder,
            QuotationConversationSubjectService conversationSubjectService,
            KeyedMutationLock<Guid> mutationLock)
        {
            _readDbContext = readDbContext;
            _writeDbContext = writeDbContext;
            _visibilityService = visibilityService;
            _dateTimeProvider = dateTimeProvider;
            _lineBuilder = lineBuilder;
            _conversationSubjectService = conversationSubjectService;
            _mutationLock = mutationLock;
        }

        public async Task<OperationResult<QuotationTotalsDto>> Handle(
            ReplaceQuotationLinesCommand command,
            CancellationToken cancellationToken)
        {
            if (command.QuotationId == Guid.Empty)
            {
                return OperationResult<QuotationTotalsDto>.Fail("QuotationId is invalid.");
            }

            using var mutationLease = await _mutationLock.AcquireAsync(
                command.QuotationId,
                cancellationToken);

            for (var attempt = 1; attempt <= MaximumSaveAttempts; attempt++)
            {
                try
                {
                    return await ReplaceOnceAsync(command, cancellationToken);
                }
                catch (DbUpdateConcurrencyException) when (attempt < MaximumSaveAttempts)
                {
                    _writeDbContext.ClearTrackedChanges();
                }
                catch (DbUpdateConcurrencyException exception)
                {
                    _writeDbContext.ClearTrackedChanges();
                    return OperationResult<QuotationTotalsDto>.Fail(
                        OptimisticConcurrencyHelper.CreateConflictMessage("Quotation", exception));
                }
            }

            return OperationResult<QuotationTotalsDto>.Fail(
                "Quotation lines could not be replaced.");
        }

        private async Task<OperationResult<QuotationTotalsDto>> ReplaceOnceAsync(
            ReplaceQuotationLinesCommand command,
            CancellationToken cancellationToken)
        {
            var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
            var quotation = await _writeDbContext.Quotations
                .AsTracking()
                .Include(x => x.Lines)
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
                    "Only a draft quotation can have its lines replaced.");
            }

            var concurrencyError = OptimisticConcurrencyHelper.ValidateExpectedUpdatedDate(
                command.Request.ExpectedUpdatedDate,
                quotation.UpdatedDate,
                "Quotation");
            if (concurrencyError is not null)
            {
                return OperationResult<QuotationTotalsDto>.Fail(concurrencyError);
            }

            var lineResult = await _lineBuilder.BuildAsync(
                quotation.QuotationId,
                scope.CompanyId,
                quotation.CustomerId,
                quotation.Currency,
                command.Request.Lines,
                cancellationToken);
            if (!lineResult.Success || lineResult.Data is null)
            {
                return OperationResult<QuotationTotalsDto>.Fail(lineResult.Message!);
            }

            var existingLines = quotation.Lines.ToList();
            _writeDbContext.QuotationLines.RemoveRange(existingLines);
            quotation.Lines.Clear();
            foreach (var line in lineResult.Data)
            {
                quotation.Lines.Add(line);
            }
            _writeDbContext.QuotationLines.AddRange(lineResult.Data);

            QuotationRules.RecalculateTotals(quotation);
            quotation.UpdatedBy = scope.EmployeeId;
            quotation.UpdatedDate = _dateTimeProvider.Now;

            await _conversationSubjectService.SyncSubjectAsync(
                quotation.QuotationId,
                quotation.CompanyId,
                quotation.ExternalId,
                quotation.Lines
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.QuotationLineId)
                    .Select(x => x.ProductExternalIdSnapshot),
                cancellationToken);

            var affectedRows = await _writeDbContext.SaveChangesAsync(cancellationToken);
            if (affectedRows == 0)
            {
                return OperationResult<QuotationTotalsDto>.Fail(
                    "Quotation line replacement was not persisted.");
            }

            return OperationResult<QuotationTotalsDto>.Ok(ToTotals(quotation), "Quotation lines replaced successfully.");
        }

        private static QuotationTotalsDto ToTotals(Quotation quotation)
            => new()
            {
                SubTotal = quotation.SubTotal,
                DiscountAmount = quotation.DiscountAmount,
                TaxPercent = quotation.TaxPercent,
                TaxAmount = quotation.TaxAmount,
                TotalAmount = quotation.TotalAmount,
                UpdatedDate = quotation.UpdatedDate
            };
    }

}
