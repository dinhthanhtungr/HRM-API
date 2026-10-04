using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Commands.ReplaceQuotationLines;

internal sealed class ReplaceQuotationLinesCommandHandler(
    QuotationLinesReplaceService service,
    ICRMWriteDbContext _writeDbContext,
    KeyedMutationLock<Guid> _mutationLock)
    : IRequestHandler<ReplaceQuotationLinesCommand, OperationResult<QuotationTotalsDto>>
{
    private const int MaximumSaveAttempts = 2;
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
                    return await service.ApplyAsync(command.QuotationId, command.Request,
                        command.Request.ExpectedUpdatedDate, persist: true, cancellationToken);
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


}